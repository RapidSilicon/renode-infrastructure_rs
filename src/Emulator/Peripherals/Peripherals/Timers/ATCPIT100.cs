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
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            DefineRegisters();
            DefineReloadRegisters();
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
                internalTimers[i].Enabled = timerEnabled[i].Value;
            }

            RequestReturnOnAllCPUs();
        }

        private void UpdateInterrupts()
        {
            for(var i = 0; i < TimersCount; ++i)
            {
                var interrupt = false;
                interrupt |= internalTimers[i].Compare0Event && internalTimers[i].Compare0Interrupt;
                if(Connections[i].IsSet != interrupt)
                {
                    this.InfoLog("Changing Interrupt{0} from {1} to {2}", i, Connections[i].IsSet, interrupt);
                }

                Connections[i].Set(interrupt);
            }
        }


        private void TimerEnable(){

            /*
             * compare ChannelN_Control_ChMode[] with the timer you are trying to enable
             * only set timer if channel mode matches which timer you are trying to enable
             * ie timer 1 cannot be set if channel is in 32bit timer mode

             * set timerEnabled[i].Value = 1;
            */
        }

        private void ReloadRegister(int channelNum, int timerNum, ulong reloadValue, ChannelMode chMode){
            /*
             * set channel n reload value depending on ChannelN_Control_ChMode[] by bitshifting values
             * 
             */
        }

         private void InterruptEnable(int channelNum, int timerNum, int interruptValue){
            /*
             * 
             * 
            */
        }

        //call define reload Registers again when changing channel mode(s) to redefine registers
        private void DefineReloadRegisters(){
            switch(ChannelN_Control_ChMode[0]){
                    case ChannelMode.Timer_32bit:
                        this.InfoLog("setting channel0 reload register to 32bit timer mode");
                        //TODO: errors when trying to redefine register: "An item with the same key has already been added. Key: 36"
                        //Solution to implement: remove register then define again - requires register to be defined upon initalization
                        //                       in separate function
                        
                        //Registers.Ch0Reload.Remove(this);

                        Registers.Ch0Reload.Define(this)
                            .WithValueField(0, 31, FieldMode.Read | FieldMode.Write, name: "TMR32_0", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_32bit));
                        break;
                    case ChannelMode.Timer_16bit:
                        this.InfoLog("setting channel0 reload register to two 16bit timers mode");
                        Registers.Ch0Reload.Define(this)
                            .WithValueField(0, 16, FieldMode.Read | FieldMode.Write, name: "TMR16_0", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_16bit))
                            .WithValueField(16, 16, FieldMode.Read | FieldMode.Write, name: "TMR16_2", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_16bit));
                        break;
                    case ChannelMode.Timer_8bit:
                        this.InfoLog("setting channel0 reload register to four 8bit timers mode");
                        Registers.Ch0Reload.Define(this)
                            .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "TMR8_0", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_8bit))
                            .WithValueField(8, 8, FieldMode.Read | FieldMode.Write, name: "TMR8_1", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_8bit))
                            .WithValueField(16, 8, FieldMode.Read | FieldMode.Write, name: "TMR8_2", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_8bit))
                            .WithValueField(24, 8, FieldMode.Read | FieldMode.Write, name: "TMR8_3", 
                            changeCallback: (_, newValue) => ReloadRegister(0, 0, newValue, ChannelMode.Timer_8bit));
                        break;
                }
            switch(ChannelN_Control_ChMode[1]){
                
            }
            switch(ChannelN_Control_ChMode[2]){
                
            }
            switch(ChannelN_Control_ChMode[3]){
                
            }

        }

        private void controlfn(){
           ulong x = (ulong) ChannelN_Control_ChMode[0];
           this.InfoLog("channelMode: {0}", x);
        }

        //define registers here, add read/write callback, define bitfields
        private void DefineRegisters()
        {
            //Read Only Register
            Registers.Cfg.Define(this)
                .WithReservedBits(3,29)
                .WithValueField(0, 3, FieldMode.Read, name:"NumCh",
                    valueProviderCallback: _ => channelCount)
            ;

            Registers.IntEn.Define(this)
                .WithReservedBits(16,16)
                .WithFlag(15, FieldMode.Read | FieldMode.Write, name: "Ch3Int3En",
                    changeCallback: (_, value) =>
                    {
                        ChannelN_InterruptM_En[3, 3] = value;
                    })
                .WithFlag(14, FieldMode.Read | FieldMode.Write, name: "Ch3Int2En",
                    changeCallback: (_, value) =>
                    {
                        ChannelN_InterruptM_En[3, 2] = value;
                    })
            ;

            Registers.IntSt.Define(this)
                // note: Write 1 to clear, use FieldMode.WriteOneToClear

                //implement R/W here using ChannelN_InterruptM_St[][]
            ;

            Registers.ChEn.Define(this)
                //implement R/W here using ChannelN_TimerM_EN[][]

                //direct changeCallback to timerEnable()
            ;

            Registers.Ch0Ctrl.Define(this) //Channel 0 Control Register
                //implement R/W here using ChannelN_Control_PWM_Park, ChannelN_Control_ChClk, ChannelN_Control_ChMode

                //direct changeCallback to timerEnable()
                .WithReservedBits(5,27)
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch0PwmPark",
                    changeCallback: (_, value) => { ChannelN_Control_PWM_Park[0] = (bool)value; })
                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch0clk",
                    changeCallback: (_, value) => { ChannelN_Control_ChClk[0] = (bool)value; })
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch0Mode", 
                    changeCallback: (_, value) => 
                    { 
                        if (Enum.IsDefined(typeof(ChannelMode), (ushort)value))
                        {
                            ChannelN_Control_ChMode[0] = (ChannelMode)(ushort)value;
                            DefineReloadRegisters(); //need to redefine reload registers based on new channel mode
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "ATCPIT100: Channel 0 unknown channel mode");
                        }
                    })
            ;

            //Channel 0 Reload Register
                //implement R/W here using ChannelN_Reload

                //direct changeCallback to reloadRegister()

                

            Registers.Ch0Cntr.Define(this) //Channel 0 Counter Register
                //implement R/W here
            ;

            Registers.Ch1Ctrl.Define(this) //Channel 1 Control Register
                .WithReservedBits(5,27)
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch1PwmPark",
                    changeCallback: (_, value) => { ChannelN_Control_PWM_Park[1] = (bool)value; })
                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch1clk",
                    changeCallback: (_, value) => { ChannelN_Control_ChClk[1] = (bool)value; })
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch1Mode", 
                    changeCallback: (_, value) => 
                    { 
                        if (Enum.IsDefined(typeof(ChannelMode), (ushort)value))
                        {
                            ChannelN_Control_ChMode[1] = (ChannelMode)(ushort)value;
                            DefineReloadRegisters(); //need to redefine reload registers based on new channel mode
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "ATCPIT100: Channel 1 unknown channel mode");
                        }
                    })

            ;


            Registers.Ch1Cntr.Define(this) //Channel 1 Counter Register
                //implement R/W here
            ;

            Registers.Ch2Ctrl.Define(this) //Channel 2 Control Register
                .WithReservedBits(5,27)
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch2PwmPark",
                    changeCallback: (_, value) => { ChannelN_Control_PWM_Park[2] = (bool)value; })
                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch2clk",
                    changeCallback: (_, value) => { ChannelN_Control_ChClk[2] = (bool)value; })
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch2Mode", 
                    changeCallback: (_, value) => 
                    { 
                        if (Enum.IsDefined(typeof(ChannelMode), (ushort)value))
                        {
                            ChannelN_Control_ChMode[2] = (ChannelMode)(ushort)value;
                            DefineReloadRegisters(); //need to redefine reload registers based on new channel mode
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "ATCPIT100: Channel 2 unknown channel mode");
                        }
                    })
            ;

            Registers.Ch2Reload.Define(this) //Channel 2 Reload Register
                //implement R/W here using ChannelN_Reload

                //direct changeCallback to reloadRegister()
            ;

            Registers.Ch2Cntr.Define(this) //Channel 2 Counter Register
                //implement R/W here
            ;

            Registers.Ch3Ctrl.Define(this) //Channel 3 Control Register
                .WithReservedBits(5,27)
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch3PwmPark",
                    changeCallback: (_, value) => { ChannelN_Control_PWM_Park[3] = (bool)value; })
                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch3clk",
                    changeCallback: (_, value) => { ChannelN_Control_ChClk[3] = (bool)value; })
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch3Mode", 
                    changeCallback: (_, value) => 
                    { 
                        if (Enum.IsDefined(typeof(ChannelMode), (ushort)value))
                        {
                            ChannelN_Control_ChMode[3] = (ChannelMode)(ushort)value;
                            DefineReloadRegisters(); //need to redefine reload registers based on new channel mode
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "ATCPIT100: Channel 3 unknown channel mode");
                        }
                    })
            ;

            Registers.Ch3Reload.Define(this) //Channel 3 Reload Register
                //implement R/W here using ChannelN_Reload

                //direct changeCallback to reloadRegister()
            ;

            Registers.Ch3Cntr.Define(this) //Channel 3 Counter Register
                //implement R/W here
            ;
        }

        private readonly InternalTimer[] internalTimers;

        private IFlagRegisterField[] timerEnabled;


        private const int channelCount = 4;

        //register values variables
        private bool [ , ] ChannelN_InterruptM_En = new bool[4, 4];             //ChannelN_InterruptM_En [0][1] is channel 0 interrupt 1 enable
        private bool [ , ] ChannelN_InterruptM_St = new bool[4, 4];             //ChannelN_InterruptM_St [0][1] is channel 0 interrupt 1 status
        private bool [ , ] ChannelN_TimerM_En = new bool[4, 4];                 //ChannelN_TimerM_En [0][1] is channel 0 timer 1 enable

        private bool [] ChannelN_Control_PWM_Park = new bool[4];                //Channel N's PWM park value
        private bool [] ChannelN_Control_ChClk = new bool[4];                   //Channel N's clock source (0 = External clock, 1 = APB Clock)
        private ChannelMode [] ChannelN_Control_ChMode = new ChannelMode[4];    //Channel N's channel mode

        private uint [] ChannelN_Reload = new uint[4];                          //Channel N's reload value(s), depends on ChannelN_Control_ChMode

        private uint [] ChannelN_Counter = new uint[4];                         //Channel N's Counter value(s), depends on ChannelN_Control_ChMode

        private const int TimersCount = 16;
         public long Size => 0x62;                          //TODO: check math

        private class InternalTimer
        {
            public InternalTimer(IPeripheral parent, IClockSource clockSource, int index)
            {
                compare0Timer = new ComparingTimer(clockSource, 1, parent, $"timer{index}cmp0", limit: 0xFFFFFFFF, compare: 0xFFFFFFFF, enabled: false);

                compare0Timer.CompareReached += () =>
                {
                    Compare0Event = true;
                    CompareReached();
                };
            }

            public void Reset()
            {
                Enabled = false;
                Compare0Event = false;
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
                }
            }

            public bool OneShot { get; set; }

            public ulong Value
            {
                get => compare0Timer.Value;
                set
                {
                    compare0Timer.Value = value;
                }
            }

            public long Frequency
            {
                get => compare0Timer.Frequency;
                set
                {
                    compare0Timer.Frequency = value;
                }
            }

            public uint Divider
            {
                get => compare0Timer.Divider;
                set
                {
                    compare0Timer.Divider = value;
                }
            }

            public ulong Compare0
            {
                get => compare0Timer.Compare;
                set => compare0Timer.Compare = value;
            }

            public bool Compare0Event { get; set; }

            public bool Compare0Interrupt
            {
                get => compare0Timer.EventEnabled;
                set => compare0Timer.EventEnabled = value;
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
        }

        private enum ChannelMode : ushort 
        {
            Timer_32bit     = 1, // one 32 bit timer 0
            Timer_16bit     = 2, // two 16 bit timers 0 - 1
            Timer_8bit      = 3, // four 8 bit timers 0 - 3
            PWM             = 4, // 16 bit PWM
            PWM_Timer_16bit = 6, // 8 bit PWM and 16 bit timer 0
            PWM_Timer_8bit  = 7 // 8 bit PWM and two 8 bit timers 0 - 1
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
