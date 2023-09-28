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
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ATCPIT100 : BasicDoubleWordPeripheral, INumberedGPIOOutput, IKnownSize
    {
        public ATCPIT100(Machine machine) : base(machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            internalTimers = new InternalTimer[TimersCount];
            for(var i = 0; i < TimersCount; ++i)
            {
                internalTimers[i] = new InternalTimer(this, machine.ClockSource, i);
                internalTimers[i].OnCompare += UpdateInterrupts;
                innerConnections[i] = new GPIO();
            }

            timerEnabled = new IFlagRegisterField[TimersCount];
            functionSelect = new IEnumRegisterField<FunctionSelect>[TimersCount];

            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < TimersCount; ++i)
            {
                internalTimers[i].Reset();
                Connections[i].Unset();
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        private void RequestReturnOnAllCPUs()
        {
            foreach(var cpu in machine.GetPeripheralsOfType<TranslationCPU>())
            {
                cpu.RequestReturn();
            }
        }

        private void UpdateTimerActiveStatus()
        {
            for(var i = 0 ; i < TimersCount; ++i)
            {
                internalTimers[i].Enabled = timerEnabled[i].Value; //use 
            }

            RequestReturnOnAllCPUs();
        }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < TimersCount; ++i)
            {
                var interrupt = false;
                interrupt |= internalTimers[i].Compare0Event && internalTimers[i].Compare0Interrupt;
                interrupt |= internalTimers[i].Compare1Event && internalTimers[i].Compare1Interrupt;

                if(Connections[i].IsSet != interrupt)
                {
                    this.NoisyLog("Changing Interrupt{0} from {1} to {2}", i, Connections[i].IsSet, interrupt);
                }

                Connections[i].Set(interrupt);
            }
        }

        



        //define registers here, add read/write callback, define bitfields
        private void DefineRegisters()
        {
            Registers.Cfg.Define(this)
                .WithReservedBits(3,31)
                .WithValueField(0, 3, FieldMode.Read, name:"NumCh",
                    valueProviderCallback: valueProviderCallback: _ => channelCount)
            ;

            Registers.IntEn.Define(this)
                .WithReservedBits(16,31)
                .WithFlag(15, FieldMode.Set, name: "Ch3Int3En",
                    changeCallback: _ => ChannelN_InterruptM_En[3][3])
                .WithFlag(14, FieldMode.Set, name: "Ch3Int2En",
                    changeCallback: _ => ChannelN_InterruptM_En[3][2])
            ;
            Registers.IntSt.Define(this)
                //implement R/W here using ChannelN_InterruptM_St[][]
            ;
            Registers.ChEn.Define(this)
                //implement R/W here using ChannelN_TimerM_EN[][]
            ;
            Registers.Ch0Ctrl.Define(this) //Channel 0 Control Register
                //implement R/W here using ChannelN_Control_PWM_Park, ChannelN_Control_ChClk, ChannelN_Control_ChMode
            ;
            Registers.Ch0Reload.Define(this) //Channel 0 Reload Register
                //implement R/W here using ChannelN_Reload
            ;
            Registers.Ch0Cntr.Define(this) //Channel 0 Counter Register
                //implement R/W here
            ;
            Registers.Ch1Ctrl.Define(this) //Channel 1 Control Register
                //implement R/W here using ChannelN_Control_PWM_Park, ChannelN_Control_ChClk, ChannelN_Control_ChMode
            ;
            Registers.Ch1Reload.Define(this) //Channel 1 Reload Register
                //implement R/W here using ChannelN_Reload
            ;
            Registers.Ch1Cntr.Define(this) //Channel 1 Counter Register
                //implement R/W here
            ;
            Registers.Ch2Ctrl.Define(this) //Channel 2 Control Register
                //implement R/W here using ChannelN_Control_PWM_Park, ChannelN_Control_ChClk, ChannelN_Control_ChMode
            ;
            Registers.Ch2Reload.Define(this) //Channel 2 Reload Register
                //implement R/W here using ChannelN_Reload
            ;
            Registers.Ch2Cntr.Define(this) //Channel 2 Counter Register
                //implement R/W here
            ;
            Registers.Ch3Ctrl.Define(this) //Channel 3 Control Register
                //implement R/W here using ChannelN_Control_PWM_Park, ChannelN_Control_ChClk, ChannelN_Control_ChMode
            ;
            Registers.Ch3Reload.Define(this) //Channel 3 Reload Register
                //implement R/W here using ChannelN_Reload
            ;
            Registers.Ch3Cntr.Define(this) //Channel 3 Counter Register
                //implement R/W here
            ;
        }

        private readonly InternalTimer[] internalTimers;

        private IFlagRegisterField[] timerEnabled;


        private const int channelCount = 4;

        //register values variables
        private const bool [4][4] ChannelN_InterruptM_En;       //ChannelN_InterruptM_En [0][1] is channel 0 interrupt 1 enable
        private const bool [4][4] ChannelN_InterruptM_St;       //ChannelN_InterruptM_St [0][1] is channel 0 interrupt 1 status
        private const bool [4][4] ChannelN_TimerM_En;           //ChannelN_TimerM_En [0][1] is channel 0 timer 1 enable

        private const bool [4] ChannelN_Control_PWM_Park;       //Channel N's PWM park value
        private const bool [4] ChannelN_Control_ChClk;          //Channel N's clock source (0 = External clock, 1 = APB Clock)
        private const ChannelMode [4] ChannelN_Control_ChMode;  //Channel N's channel mode

        private const uint [4] ChannelN_Reload;                 //Channel N's reload value(s), depends on ChannelN_Control_ChMode

        private const uint [4] ChannelN_Counter;                //Channel N's Counter value(s), depends on ChannelN_Control_ChMode

        private const int TimersCount = 16;

        private class InternalTimer
        {
            public InternalTimer(IPeripheral parent, IClockSource clockSource, int index)
            {
                compare0Timer = new ComparingTimer(clockSource, 1, parent, $"timer{index}cmp0", limit: 0xFFFFFFFF, compare: 0xFFFFFFFF, enabled: false);
                compare1Timer = new ComparingTimer(clockSource, 1, parent, $"timer{index}cmp1", limit: 0xFFFFFFFF, compare: 0xFFFFFFFF, enabled: false);

                compare0Timer.CompareReached += () =>
                {
                    Compare0Event = true;
                    CompareReached();
                };
                compare1Timer.CompareReached += () =>
                {
                    Compare1Event = true;
                    CompareReached();
                };
            }

            public void Reset()
            {
                Enabled = false;
                Compare0Event = false;
                Compare1Event = false;
            }

            public bool Enabled
            {
                get => compare0Timer.Enabled;
                set
                {
                    if(Enabled == value)
                    {
                        return;
                    }

                    Value = 0;
                    compare0Timer.Enabled = value;
                    compare1Timer.Enabled = value;
                }
            }

            public bool OneShot { get; set; }

            public ulong Value
            {
                get => compare0Timer.Value;
                set
                {
                    compare0Timer.Value = value;
                    compare1Timer.Value = value;
                }
            }

            public long Frequency
            {
                get => compare0Timer.Frequency;
                set
                {
                    compare0Timer.Frequency = value;
                    compare1Timer.Frequency = value;
                }
            }

            public uint Divider
            {
                get => compare0Timer.Divider;
                set
                {
                    compare0Timer.Divider = value;
                    compare1Timer.Divider = value;
                }
            }

            public ulong Compare0
            {
                get => compare0Timer.Compare;
                set => compare0Timer.Compare = value;
            }

            public ulong Compare1
            {
                get => compare1Timer.Compare;
                set => compare1Timer.Compare = value;
            }

            public bool Compare0Event { get; set; }
            public bool Compare1Event { get; set; }

            public bool Compare0Interrupt
            {
                get => compare0Timer.EventEnabled;
                set => compare0Timer.EventEnabled = value;
            }

            public bool Compare1Interrupt
            {
                get => compare1Timer.EventEnabled;
                set => compare1Timer.EventEnabled = value;
            }

            public Action OnCompare;

            private void CompareReached()
            {
                OnCompare?.Invoke();

                if(OneShot)
                {
                    Value = 0;
                }
            }

            private readonly ComparingTimer compare0Timer;
            private readonly ComparingTimer compare1Timer;
        }

        private enum ChannelMode : ushort 
        {
            Timer_32bit     = 1, // one 32 bit timer 0
            Timer_16bit     = 2, // two 16 bit timers 0 - 1
            Timer_8bit      = 3, // four 8 bit timers 0 - 3
            PWM             = 4, // 16 bit PWM
            PWM_Timer_16bit = 6, // 8 bit PWM and 16 bit timer 0
            PWM_Timer_8bit  = 7; // 8 bit PWM and two 8 bit timers 0 - 1
        }

        private enum Registers : long
        {
            IdRev = 0x00,       //ID and Revision Register
            Cfg   = 0x10,       //Configuration Register
            IntEn = 0x14,       //Interrupt Enable Register
            IntSt = 0x18,       //Interrupt Status Register

            ChEn  = 0x1C,       //Channel Enable Register

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
    }
}
