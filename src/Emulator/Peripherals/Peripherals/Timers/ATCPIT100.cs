//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.Timers
{
    // This model does not replicate the behavior of the IP when the advise of 
    // disabling a channel before changing configuration is ignored as RTL would
    // be required to replicate it. 
    public class ATCPIT100 : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
    {
        public ATCPIT100(Machine machine, long frequencyExt, long frequencyAPB, int channelCount = 4) : base(machine)
        {
            this.channelCount = channelCount;
            this.chnPwmPark = new bool[channelCount]; //Channel N's PWM park value
            this.chnMode = new ChannelMode[channelCount];    //Channel N's channel mode
            //? -- Does channel mode change cause inmediate eval of timer reload
            //?    and Enable, and interrupt enable? R/ Only eval reload val
            //? -- Do the registers for interrupt enable and timer enable save incorrect
            //?    Values or do they ignore invalid toggles to true? 
            //?    R/ They save the incorrect values for intr_en but not ch_en 
            IRQ = new GPIO();
            Ch0PWM = new GPIO();
            Ch1PWM = new GPIO();
            Ch2PWM = new GPIO();
            Ch3PWM = new GPIO();
            chPwm = new GPIO[4];
            chPwm[0] = Ch0PWM;
            chPwm[1] = Ch1PWM;
            chPwm[2] = Ch2PWM;
            chPwm[3] = Ch3PWM;

            internalTimers = new InternalTimer[channelCount, 6];

            for (var i = 0; i < channelCount; ++i)
            {
                for (var j = 0; j < 4; ++j)
                {
                    internalTimers[i, j] = new InternalTimer(
                        machine.ClockSource,
                        frequencyExt,
                        this,
                        $"Chn{i}_Tmr{j}"
                    );
                    internalTimers[i, j].LimitReached += () =>
                    {
                        refreshIRQ();
                    };
                }

                internalTimers[i, 4] = new InternalTimer(
                    machine.ClockSource,
                    frequencyExt,
                    this,
                    $"Chn{i}_PMM_Lo"
                );
                internalTimers[i, 5] = new InternalTimer(
                    machine.ClockSource,
                    frequencyExt,
                    this,
                    $"Chn{i}_PWM_Hi"
                );

                internalTimers[i, 4].LimitReached += () =>
                {
                    internalTimers[i, 4].Enabled = false;
                    internalTimers[i, 5].Enabled = true;
                    chPwm[i].Set(true);
                };
                internalTimers[i, 5].LimitReached += () =>
                {
                    internalTimers[i, 4].Enabled = true;
                    internalTimers[i, 5].Enabled = false;
                    chPwm[i].Set(false);
                };
            }
            this.frequencyExt = frequencyExt;
            this.frequencyAPB = frequencyAPB;

            DefineRegisters();
            Reset();
        }

        private void refreshIRQ(){
            ulong intEn = RegistersCollection.Read((long)Registers.IntEn);
            ulong intSt = RegistersCollection.Read((long)Registers.IntSt);
            IRQ.Set((intEn & intSt)!=0);
        }
        public override void Reset()
        {
            base.Reset();
            for (var i = 0; i < channelCount; ++i)
            {

                chnMode[i] = 0; // Init to invalid value
                for (var j = 0; j < timersPerChannel; ++j)
                {
                    internalTimers[i, j].Reset();
                }
            }
            IRQ.Unset();
        }

        public void OnGPIO(int number, bool value)
        {
            // check only for gpio pause signal in gpio 0
            if (number == 0)
            {
                this.Log(LogLevel.Info, "PIT Pause signal value changed");
                for (var i = 0; i < channelCount; ++i)
                {
                    for (var j = 0; j < timersPerChannel; ++j)
                    {
                        internalTimers[i, j].Pause(value);
                    }
                }
            }
        }

        private void TimerEnableChange(int channel, int timer, bool enableValue)
        {
            var mode = chnMode[channel];
            Action validCaseAction = () =>
            {
                internalTimers[channel, timer].Enable = enableValue;
                this.Log(LogLevel.Info, $"Channel {channel} Timer {timer} enabled: {enableValue}");
            };
            Action invalidCaseAction = () =>
            {
                //? Is this value written to regardless?
                //? R/ No, RTL does not allow the bit to change if the mode is invalid
                if (enableValue)
                {
                    this.Log(LogLevel.Error,
                        $"Cannot enable timer {timer} when channel {channel} is in {mode} mode");
                }
            };
            switch (timer)
            {
                case 0:
                    switch (mode)
                    {
                        case ChannelMode.Timer_32bit:
                        case ChannelMode.Timer_16bit:
                        case ChannelMode.Timer_8bit:
                        case ChannelMode.PWM_Timer_16bit:
                        case ChannelMode.PWM_Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 1:
                    switch (mode)
                    {
                        case ChannelMode.Timer_16bit:
                        case ChannelMode.Timer_8bit:
                        case ChannelMode.PWM_Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 2:
                    switch (mode)
                    {
                        case ChannelMode.Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 3:
                    switch (mode)
                    {
                        case ChannelMode.PWM:
                        case ChannelMode.PWM_Timer_16bit:
                        case ChannelMode.PWM_Timer_8bit:
                            if (enableValue)
                            {
                                chPwm[channel].Unset(); // Start low cycle
                            }
                            else
                            {
                                chPwm[channel].Set(chnPwmPark[channel]);
                            }
                            internalTimers[channel, 4].Enable = enableValue;
                            this.Log(LogLevel.Info, $"Channel {channel} PWM enabled: {enableValue}");
                            break;
                        case ChannelMode.Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                default:
                    break;

            }
        }

        private bool TimerEnableValue(int channel, int timer)
        {
            if (timer != 3)
            {
                return internalTimers[channel, timer].Enable;
            }
            else
            {
                switch (chnMode[channel])
                {
                    case ChannelMode.PWM:
                    case ChannelMode.PWM_Timer_16bit:
                    case ChannelMode.PWM_Timer_8bit:
                        return (internalTimers[channel, 4].Enable ||
                                internalTimers[channel, 5].Enable);
                    default:
                        return internalTimers[channel, 3].Enable;
                }
            }
        }

        private void ReloadRegister(int channel, ulong reloadValue)
        {
            ulong reload32bit0 = BitHelper.GetValue(reloadValue, 0, 32);
            ulong reload16bit0 = BitHelper.GetValue(reloadValue, 0, 16);
            ulong reload16bit1 = BitHelper.GetValue(reloadValue, 16, 16);
            ulong reload8bit0 = BitHelper.GetValue(reloadValue, 0, 8);
            ulong reload8bit1 = BitHelper.GetValue(reloadValue, 8, 8);
            ulong reload8bit2 = BitHelper.GetValue(reloadValue, 16, 8);
            ulong reload8bit3 = BitHelper.GetValue(reloadValue, 24, 8);

            switch (chnMode[channel])
            {
                case ChannelMode.Timer_32bit:
                    internalTimers[channel, 0].Reload = reload32bit0;
                    break;
                case ChannelMode.Timer_16bit:
                    internalTimers[channel, 0].Reload = reload16bit0;
                    internalTimers[channel, 1].Reload = reload16bit1;
                    break;
                case ChannelMode.Timer_8bit:
                    internalTimers[channel, 0].Reload = reload8bit0;
                    internalTimers[channel, 1].Reload = reload8bit1;
                    internalTimers[channel, 2].Reload = reload8bit2;
                    internalTimers[channel, 3].Reload = reload8bit3;
                    break;
                case ChannelMode.PWM:
                    internalTimers[channel, 4].Reload = reload16bit0;
                    internalTimers[channel, 5].Reload = reload16bit1;
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    internalTimers[channel, 0].Reload = reload16bit0;
                    internalTimers[channel, 4].Reload = reload8bit2;
                    internalTimers[channel, 5].Reload = reload8bit3;
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    internalTimers[channel, 0].Reload = reload8bit0;
                    internalTimers[channel, 1].Reload = reload8bit1;
                    internalTimers[channel, 4].Reload = reload8bit2;
                    internalTimers[channel, 5].Reload = reload8bit3;
                    break;
                default:
                    this.Log(LogLevel.Error, $"Channel {channel} invalid mode detected while reloading");
                    break;
            }
            this.Log(LogLevel.Info, "setting ch{0} reload value to 0x{1:X} with channel mode {2}",
                        channel, reloadValue, chnMode[channel]);
        }

        private void InterruptEnable(int channel, int timer, bool enableValue)
        {
            var mode = chnMode[channel];
            Action validCaseAction = () =>
            {
                internalTimers[channel, timer].InterruptEnable = enableValue;
                this.Log(LogLevel.Info, $"Channel {channel} Timer {timer} irq enabled: {enableValue}");
            };
            Action invalidCaseAction = () =>
            {
                //? Is this value written to regardless?
                //? R/ Yes it is
                internalTimers[channel, timer].InterruptEnable = enableValue;
                if (enableValue)
                {
                    this.Log(LogLevel.Error,
                        $"Cannot enable irq in timer {timer} when channel {channel} is in {mode} mode");
                }
            };
            switch (timer)
            {
                case 0:
                    switch (mode)
                    {
                        case ChannelMode.Timer_32bit:
                        case ChannelMode.Timer_16bit:
                        case ChannelMode.Timer_8bit:
                        case ChannelMode.PWM_Timer_16bit:
                        case ChannelMode.PWM_Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 1:
                    switch (mode)
                    {
                        case ChannelMode.Timer_16bit:
                        case ChannelMode.Timer_8bit:
                        case ChannelMode.PWM_Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 2:
                    switch (mode)
                    {
                        case ChannelMode.Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                case 3:
                    switch (mode)
                    {
                        case ChannelMode.PWM:
                        case ChannelMode.PWM_Timer_16bit:
                        case ChannelMode.PWM_Timer_8bit:
                            this.Log(LogLevel.Warning, "irq case for pwm needs clarification");
                            break;
                        case ChannelMode.Timer_8bit:
                            validCaseAction();
                            break;
                        default:
                            invalidCaseAction();
                            break;
                    }
                    break;
                default:
                    break;

            }
            refreshIRQ();
        }

        private ulong CounterValue(int channel)
        {
            ulong ret = 0;
            switch (chnMode[channel])
            {
                case ChannelMode.Timer_32bit:
                    ret = internalTimers[channel, 0].Count;
                    break;
                case ChannelMode.Timer_16bit:
                    ret = internalTimers[channel, 0].Count |
                        (internalTimers[channel, 1].Count << 16);
                    break;
                case ChannelMode.Timer_8bit:
                    ret = internalTimers[channel, 0].Count |
                        (internalTimers[channel, 1].Count << 8) |
                        (internalTimers[channel, 2].Count << 16) |
                        (internalTimers[channel, 3].Count << 24);
                    break;
                case ChannelMode.PWM:
                    ret = internalTimers[channel, 4].Count |
                        (internalTimers[channel, 5].Count << 16);
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    ret = internalTimers[channel, 0].Count |
                        (internalTimers[channel, 4].Count << 16) |
                        (internalTimers[channel, 5].Count << 24);
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    ret = internalTimers[channel, 0].Count |
                        (internalTimers[channel, 1].Count << 8) |
                        (internalTimers[channel, 4].Count << 16) |
                        (internalTimers[channel, 5].Count << 24);
                    break;
                default:
                    this.Log(LogLevel.Error, $"Channel {channel} invalid mode detected while reading counter");
                    break;
            }
            return ret;

        }

        private void ChangeChannelMode(int channel, ulong channelModeVal)
        {
            // Even if the datasheet says the channel must be disabled, the channel 
            // mode change is still performed. 
            if (!Enum.IsDefined(typeof(ChannelMode), channelModeVal))
            {
                this.Log(LogLevel.Error, $"Channel {channel} has been configured with reserved channel mode {channelModeVal}");
            }
            chnMode[channel] = (ChannelMode)channelModeVal;

            // Disable unused channels and Reset their Value;
            // Channel 0 is never disabled on change mode 

            // refresh reload values for timer bassed on current Reload Value
            ReloadRegister(channel, RegistersCollection.Read(
                (long)Registers.Ch0Reload + (long)channel * 0x10));

            //? refresh interrupt enable values? R/ No, RTL does not
            //? refresh interrupt status values? R/ No, RTL does not 

        }

        private void ChangeChannelClk(int channel, bool apbClock)
        {
            long frequency = apbClock ? frequencyAPB : frequencyExt;
            for (int i = 0; i < 6; i++)
            {
                internalTimers[channel, i].Frequency = frequency;
            }
        }

        private void InterruptStatusChange(int channel, int timer, bool newVal)
        {
            if (newVal) internalTimers[0, timer].ClearInterrupt();
            refreshIRQ();
        }

        //define registers, read/write callback, bitfields
        private void DefineRegisters()
        {
            //Read Only Register
            Registers.Cfg.Define(this)
                .WithReservedBits(3, 29)
                .WithValueField(0, 3, FieldMode.Read, name: "NumCh",
                    valueProviderCallback: _ => (ulong)channelCount)
            ;

            //Interrupt Enable Register - 0x14
            Registers.IntEn.Define(this, 0x0)
                .WithFlags(0, 4, name: "Chn0IntEn",
                    changeCallback: (timer, _, newVal) => InterruptEnable(0, timer, newVal))
                .WithFlags(4, 4, name: "Chn1IntEn",
                    changeCallback: (timer, _, newVal) => InterruptEnable(1, timer, newVal))
                .WithFlags(8, 4, name: "Chn2IntEn",
                    changeCallback: (timer, _, newVal) => InterruptEnable(2, timer, newVal))
                .WithFlags(12, 4, name: "Chn3IntEn",
                    changeCallback: (timer, _, newVal) => InterruptEnable(3, timer, newVal))
                .WithReservedBits(16, 16);

            //Interrupt Status Register
            Registers.IntSt.Define(this)
                .WithFlags(0, 4, name: "Chn0IntSt",
                    writeCallback: (timer, _, newVal) => InterruptStatusChange(0, timer, newVal),
                    valueProviderCallback: (timer, _) => internalTimers[0, timer].InterruptStatus)
                .WithFlags(4, 4, name: "Chn1IntSt",
                    writeCallback: (timer, _, newVal) => InterruptStatusChange(1, timer, newVal),
                    valueProviderCallback: (timer, _) => internalTimers[1, timer].InterruptStatus)
                .WithFlags(8, 4, name: "Chn2IntSt",
                    writeCallback: (timer, _, newVal) => InterruptStatusChange(2, timer, newVal),
                    valueProviderCallback: (timer, _) => internalTimers[2, timer].InterruptStatus)
                .WithFlags(12, 4, name: "Chn3IntSt",
                    writeCallback: (timer, _, newVal) => InterruptStatusChange(3, timer, newVal),
                    valueProviderCallback: (timer, _) => internalTimers[3, timer].InterruptStatus)
                .WithReservedBits(16, 16);

            //Channel/Timer Enable Register - 0x1C
            Registers.ChEn.Define(this)
                .WithFlags(0, 4, name: "Ch0TmrEn", changeCallback:
                    (timer, _, newVal) => TimerEnableChange(0, timer, newVal),
                    valueProviderCallback: (timer, _) => TimerEnableValue(0, timer))
                .WithFlags(4, 4, name: "Ch1TmrEn", changeCallback:
                    (timer, _, newVal) => TimerEnableChange(1, timer, newVal),
                    valueProviderCallback: (timer, _) => TimerEnableValue(1, timer))
                .WithFlags(8, 4, name: "Ch2TmrEn", changeCallback:
                    (timer, _, newVal) => TimerEnableChange(2, timer, newVal),
                    valueProviderCallback: (timer, _) => TimerEnableValue(2, timer))
                .WithFlags(12, 4, name: "Ch3TmrEn", changeCallback:
                    (timer, _, newVal) => TimerEnableChange(3, timer, newVal),
                    valueProviderCallback: (timer, _) => TimerEnableValue(3, timer))
                .WithReservedBits(16, 16);


            //Channel 0 Control Register
            Registers.Ch0Ctrl.Define(this, 0x0)
                .WithValueField(0, 3, name: "Ch0Mode", changeCallback:
                    (_, newVal) => ChangeChannelMode(0, newVal))
                .WithFlag(3, name: "Ch0clk", changeCallback:
                    (_, newVal) => ChangeChannelClk(0, newVal))
                .WithFlag(4, name: "Ch0PwmPark",
                    changeCallback: (_, newVal) => chnPwmPark[0] = newVal)
                .WithReservedBits(5, 27);

            //Channel 0 Reload Register
            Registers.Ch0Reload.Define(this)
                .WithValueField(0, 32, name: "TMR32_0", changeCallback:
                    (_, newValue) => ReloadRegister(0, newValue));

            //Channel 0 Counter Register
            Registers.Ch0Cntr.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TMR32_0",
                    valueProviderCallback: _ => CounterValue(0));

            //Channel 1 Control Register
            Registers.Ch1Ctrl.Define(this)
                .WithValueField(0, 3, name: "Ch1Mode", changeCallback:
                    (_, newVal) => ChangeChannelMode(1, newVal))
                .WithFlag(3, name: "Ch1clk", changeCallback:
                    (_, newVal) => ChangeChannelClk(1, newVal))
                .WithFlag(4, name: "Ch1PwmPark",
                    changeCallback: (_, newVal) => chnPwmPark[1] = newVal)
                .WithReservedBits(5, 27);

            //Channel 1 Reload Register
            Registers.Ch1Reload.Define(this)
                .WithValueField(0, 32, name: "TMR32_0", changeCallback:
                    (_, newValue) => ReloadRegister(1, newValue));

            //Channel 1 Counter Register
            Registers.Ch1Cntr.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TMR32_0",
                    valueProviderCallback: _ => CounterValue(1));

            //Channel 2 Control Register
            Registers.Ch2Ctrl.Define(this)
                .WithValueField(0, 3, name: "Ch2Mode", changeCallback:
                    (_, newVal) => ChangeChannelMode(2, newVal))
                .WithFlag(3, name: "Ch2clk", changeCallback:
                    (_, newVal) => ChangeChannelClk(2, newVal))
                .WithFlag(4, name: "Ch2PwmPark",
                    changeCallback: (_, newVal) => chnPwmPark[2] = newVal)
                .WithReservedBits(5, 27);

            //Channel 2 Reload Register
            Registers.Ch2Reload.Define(this)
                .WithValueField(0, 32, name: "TMR32_0", changeCallback:
                    (_, newValue) => ReloadRegister(2, newValue));

            //Channel 2 Counter Register
            Registers.Ch2Cntr.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TMR32_0",
                    valueProviderCallback: _ => CounterValue(2));

            //Channel 3 Control Register
            Registers.Ch3Ctrl.Define(this)
                .WithValueField(0, 3, name: "Ch3Mode", changeCallback:
                    (_, newVal) => ChangeChannelMode(3, newVal))
                .WithFlag(3, name: "Ch3clk", changeCallback:
                    (_, newVal) => ChangeChannelClk(3, newVal))
                .WithFlag(4, name: "Ch3PwmPark",
                    changeCallback: (_, newVal) => chnPwmPark[3] = newVal)
                .WithReservedBits(5, 27);

            //Channel 3 Reload Register
            Registers.Ch3Reload.Define(this)
                .WithValueField(0, 32, name: "TMR32_0", changeCallback:
                    (_, newValue) => ReloadRegister(3, newValue));

            //Channel 3 Counter Register
            Registers.Ch3Cntr.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "TMR32_0",
                    valueProviderCallback: _ => CounterValue(3));

        }

        public GPIO IRQ { get; set; }
        public GPIO Ch0PWM { get; set; }
        public GPIO Ch1PWM { get; set; }
        public GPIO Ch2PWM { get; set; }
        public GPIO Ch3PWM { get; set; }

        private readonly GPIO[] chPwm;

        private readonly InternalTimer[,] internalTimers;

        private readonly int channelCount;
        private const int timersPerChannel = 6;
        public long Size => 0x62;
        private readonly long frequencyExt;
        private readonly long frequencyAPB;
        private bool[] chnPwmPark;
        private ChannelMode[] chnMode;    //Channel N's channel mode

        private enum ChannelMode : ulong
        {
            //reserverd     = 0
            Timer_32bit = 1, // one 32 bit timer 0
            Timer_16bit = 2, // two 16 bit timers 0 - 1
            Timer_8bit = 3, // four 8 bit timers 0 - 3
            //reserverd     = 5
            PWM = 4, // 16 bit PWM
            PWM_Timer_16bit = 6, // 8 bit PWM and 16 bit timer 0
            PWM_Timer_8bit = 7 // 8 bit PWM and two 8 bit timers 0 - 1
        }

        private enum Registers : long
        {
            IdRev = 0x00,       //ID and Revision Register
            Cfg = 0x10,       //Configuration Register
            IntEn = 0x14,       //Interrupt Enable Register
            IntSt = 0x18,       //Interrupt Status Register

            ChEn = 0x1C,       //Channel Enable Register

            Ch0Ctrl = 0x20,     //Channel 0 Control Register
            Ch0Reload = 0x24,   //Channel 0 Reload Register
            Ch0Cntr = 0x28,     //Channel 0 Counter Register

            Ch1Ctrl = 0x30,
            Ch1Reload = 0x34,
            Ch1Cntr = 0x38,

            Ch2Ctrl = 0x40,
            Ch2Reload = 0x44,
            Ch2Cntr = 0x48,

            Ch3Ctrl = 0x50,
            Ch3Reload = 0x54,
            Ch3Cntr = 0x58
        }

        private class InternalTimer : LimitTimer
        {
            public InternalTimer(
                IClockSource clockSource,
                long frequency,
                IPeripheral owner,
                string localName) :
                base(clockSource, frequency, owner, localName, autoUpdate: true)
            {
                paused = false;
                hasNonZeroLimit = false;
            }
            public void Pause(bool value)
            {
                paused = value;
                Enable = value;
            }

            public ulong Reload
            {
                get => (hasNonZeroLimit) ? Limit : 0;
                set
                {
                    if (value != 0)
                    {
                        hasNonZeroLimit = true;
                        Limit = value;
                    }
                    else
                    {
                        hasNonZeroLimit = false;
                    }
                }
            }
            public ulong Count
            {
                get => hasNonZeroLimit ? Value : 0;
            }
            public bool Enable
            {
                get { return Enabled; }
                set
                {
                    if(!hasNonZeroLimit) Limit = 1;
                    if (!paused) Enabled = value;
                }
            }
            public bool InterruptEnable
            {
                get { return EventEnabled; }
                set
                {
                    EventEnabled = value;
                }

            }
            public bool InterruptStatus
            {
                get { return Interrupt; }
            }

            bool paused;
            bool hasNonZeroLimit;
        }
    }
}
