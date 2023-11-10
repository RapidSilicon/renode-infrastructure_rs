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
    public class ATCPIT100 : BasicDoubleWordPeripheral, IKnownSize
    {
        public ATCPIT100(Machine machine) : base(machine)
        {
            var innerConnections = new Dictionary<int, IGPIO>();
            IRQ = new GPIO();
            internalTimers = new InternalTimer[channelCount, InternalTimersPerChannel];

            this.Log(LogLevel.Info, "clock source: {0}", machine.ClockSource);

            for(var i = 0; i < channelCount; ++i)
            {
                for (var j = 0; j < InternalTimersPerChannel; ++j)
                {
                    ulong limit = getLimit(j);
                    internalTimers[i, j] = new InternalTimer(this, machine.ClockSource, i, j, limit);
                    internalTimers[i, j].OnCompare += UpdateInterrupts;
                }
            }

            for(var i = 0; i < channelCount * InternalTimersPerChannel; ++i)
            {
                innerConnections[i] = new GPIO();
            }
                 

            DefineRegisters();
            ResetReloadRegs();
            Reset();
        }

        public GPIO IRQ { get; set; }

        private ulong getLimit(int timerNum){
            switch(timerNum){
                case 0:
                    return 0xFFFFFFFF;
                case 1:
                    return 0xFFFF;
                case 2:
                    return 0xFFFF;
                case 3:
                    return 0xFF;
                case 4:
                    return 0xFF;
                case 5:
                    return 0xFF;
                case 6:
                    return 0xFF;
                default: return 0xFFFFFFFF;
            }
        }

        public override void Reset()
        {
            base.Reset();
            for(var i = 0; i < channelCount; ++i)
            {
                for(var j = 0; j < InternalTimersPerChannel; ++j)
                {
                    internalTimers[i, j].Reset();
                }
            }
            IRQ.Unset();
        }


        private void ResetReloadRegs(){
            for (var i = 0; i < channelCount; i++){
                ChannelN_Control_ChMode[i] = ChannelMode.Timer_32bit; //initialize to ChannelMode.Timer_32bit
            }
        }

        private void RequestReturnOnAllCPUs()
        {
            foreach(var cpu in machine.GetPeripheralsOfType<TranslationCPU>())
            {
                cpu.RequestReturn();
            }
        }

        private void UpdateInterrupts()
        {
            var interrupt = false;
            for(var i = 0; i < channelCount; i++){
                for(var j = 0; j < TimersPerChannel; j++){          
                    interrupt |= (ChannelN_InterruptM_St[i, j] && ChannelN_InterruptM_En[i, j]);

                    if(IRQ.IsSet != interrupt)
                    {
                        this.InfoLog("Changing IRQ from {0} to {1}", IRQ.IsSet, interrupt);
                        //this.InfoLog("ChannelN_InterruptM_St = {0}, ChannelN_InterruptM_En = {1} i = {2}, j = {3}", ChannelN_InterruptM_St[i, j], ChannelN_InterruptM_En[i, j], i, j);
                    }
                }
            }
            IRQ.Set(interrupt);
        }        

        private void TimerEnable(int channelNum, int timerNum, bool enableValue){
            switch (ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    if (timerNum == 0){
                        ChannelN_TimerM_En[channelNum, timerNum] = enableValue;
                        internalTimers[channelNum, timerNum].Enabled = ChannelN_TimerM_En[channelNum, timerNum];
                        this.InfoLog("Enabling/disabling ch{0} timer{1}'s with value {2}, channel mode {3}", 
                            channelNum, timerNum, enableValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable timer {0} when channel {1} is in {2} mode", 
                            timerNum, channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.Timer_16bit:
                    if ((timerNum == 0) || (timerNum == 1)){
                        ChannelN_TimerM_En[channelNum, timerNum] = enableValue;
                        internalTimers[channelNum, timerNum + 1].Enabled = ChannelN_TimerM_En[channelNum, timerNum];
                        this.InfoLog("Enabling/disabling ch{0} timer{1}'s with value {2}, channel mode {3}", 
                            channelNum, timerNum, enableValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable timer {0} when channel {1} is in {2} mode", 
                            timerNum, channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.Timer_8bit:
                    if ((timerNum >= 0) && (timerNum <= 3)){
                        ChannelN_TimerM_En[channelNum, timerNum] = enableValue;
                        internalTimers[channelNum, timerNum + 3].Enabled = ChannelN_TimerM_En[channelNum, timerNum];
                        this.InfoLog("Enabling/disabling ch{0} timer{1}'s with value {2}, channel mode {3}", 
                            channelNum, timerNum, enableValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable timer {0} when channel {1} is in {2} mode", 
                            timerNum, channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.PWM:
                    //TODO: Set PWM enable value
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    //TODO: Set PWM enable value
                    if (timerNum == 0){
                        ChannelN_TimerM_En[channelNum, timerNum] = enableValue;
                        internalTimers[channelNum, 0].Enabled = ChannelN_TimerM_En[channelNum, timerNum];
                        this.InfoLog("Enabling/disabling ch{0} timer{1}'s with value {2}, channel mode {3}", 
                            channelNum, timerNum, enableValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable timer {0} when channel {1} is in {2} mode", 
                            timerNum, channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    //TODO: Set PWM enable value
                    if ((timerNum == 0) || (timerNum == 1)){
                        ChannelN_TimerM_En[channelNum, timerNum] = enableValue;
                        internalTimers[channelNum, timerNum + 1].Enabled = ChannelN_TimerM_En[channelNum, timerNum];
                        this.InfoLog("Enabling/disabling ch{0} timer{1}'s with value {2}, channel mode {3}", 
                            channelNum, timerNum, enableValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable timer {0} when channel {1} is in {2} mode", 
                            timerNum, channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
            }
            RequestReturnOnAllCPUs();
        }

        private bool TimerEnableReturn(int channelNum, int timerNum){
            RequestReturnOnAllCPUs();
            bool returnVal = false;
            switch (ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    ChannelN_TimerM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum].Enabled;
                    returnVal = ChannelN_TimerM_En[channelNum, timerNum];
                    break;
                case ChannelMode.Timer_16bit:
                    ChannelN_TimerM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 1].Enabled;
                    returnVal = ChannelN_TimerM_En[channelNum, timerNum];
                    break;
                case ChannelMode.Timer_8bit:
                    ChannelN_TimerM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 3].Enabled;
                    returnVal = ChannelN_TimerM_En[channelNum, timerNum];                    
                    break;
                case ChannelMode.PWM:
                    //TODO: Set PWM enable value
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    //TODO: Set PWM enable value
                    ChannelN_TimerM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 1].Enabled;
                    returnVal = ChannelN_TimerM_En[channelNum, timerNum];                  
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    //TODO: Set PWM enable value
                    ChannelN_TimerM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 3].Enabled;
                    returnVal = ChannelN_TimerM_En[channelNum, timerNum];                    
                    break;

            }
            return returnVal;
        }

        private void ReloadRegister(int channelNum, ulong reloadValue){ //TODO: should we check if timer is en before reloading?
            /*
             * set channel n reload value depending on ChannelN_Control_ChMode[] by bitshifting values
             */

             switch(ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    ChannelN_Reload[channelNum, 0] = (uint)(reloadValue & 0xFFFFFFFF);

                    internalTimers[channelNum, 0].Compare0 = ChannelN_Reload[channelNum, 0];
                    break;
                case ChannelMode.Timer_16bit:
                    ChannelN_Reload[channelNum, 0] = (uint)(reloadValue & 0x0000FFFF);
                    ChannelN_Reload[channelNum, 1] = (uint)((reloadValue & 0xFFFF0000) >> 16);

                    internalTimers[channelNum, 1].Compare0 = ChannelN_Reload[channelNum, 0];
                    internalTimers[channelNum, 2].Compare0 = ChannelN_Reload[channelNum, 1];
                    break;
                case ChannelMode.Timer_8bit:
                    ChannelN_Reload[channelNum, 0] = (uint)(reloadValue & 0x000000FF);
                    ChannelN_Reload[channelNum, 1] = (uint)((reloadValue & 0x0000FF00) >> 8);
                    ChannelN_Reload[channelNum, 2] = (uint)((reloadValue & 0x00FF0000) >> 16);
                    ChannelN_Reload[channelNum, 3] = (uint)((reloadValue & 0xFF000000) >> 24);

                    internalTimers[channelNum, 3].Compare0 = ChannelN_Reload[channelNum, 0];
                    internalTimers[channelNum, 4].Compare0 = ChannelN_Reload[channelNum, 1];
                    internalTimers[channelNum, 5].Compare0 = ChannelN_Reload[channelNum, 2];
                    internalTimers[channelNum, 6].Compare0 = ChannelN_Reload[channelNum, 3];
                    break;
            }
            this.InfoLog("setting ch{0} reload value to 0x{1:X} with channel mode {2}", 
                        channelNum, reloadValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
        }

        private uint ReloadRegisterReturn(int channelNum){ //TODO
            switch(ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    ChannelN_Reload[channelNum, 0] = (uint) internalTimers[channelNum, 0].Compare0;
                    break;
                case ChannelMode.Timer_16bit:
                    ChannelN_Reload[channelNum, 0] = (uint) internalTimers[channelNum, 1].Compare0 & 0xFFFF;
                    ChannelN_Reload[channelNum, 1] = (uint) internalTimers[channelNum, 2].Compare0 & 0xFFFF;
                    break;
                case ChannelMode.Timer_8bit:
                    ChannelN_Reload[channelNum, 0] = (uint) internalTimers[channelNum, 3].Compare0 & 0xFF;
                    ChannelN_Reload[channelNum, 1] = (uint) internalTimers[channelNum, 4].Compare0 & 0xFF;
                    ChannelN_Reload[channelNum, 2] = (uint) internalTimers[channelNum, 5].Compare0 & 0xFF;
                    ChannelN_Reload[channelNum, 3] = (uint) internalTimers[channelNum, 6].Compare0 & 0xFF;
                    break;
            }


            return (ChannelN_Reload[channelNum, 0] & 0xFF) | ((ChannelN_Reload[channelNum, 1] & 0xFF) << 8)
                     | ((ChannelN_Reload[channelNum, 2] & 0xFF) << 16)  | ((ChannelN_Reload[channelNum, 3] & 0xFF) << 24);
        }

        private uint CounterRegisterReturn(int channelNum){
            return 0; //temp
        } //TODO

        private void InterruptEnable(int channelNum, int timerNum, bool interruptValue){
            switch (ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    if (timerNum == 0){
                        ChannelN_InterruptM_En[channelNum, timerNum] = interruptValue;
                        internalTimers[channelNum, timerNum].Compare0Interrupt = ChannelN_InterruptM_En[channelNum, timerNum];
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable interrupt of timer0 when channel {0} is in {1} mode", 
                            channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.Timer_16bit:
                    if ((timerNum == 0) || (timerNum == 1)){
                        ChannelN_InterruptM_En[channelNum, timerNum] = interruptValue;
                        internalTimers[channelNum, timerNum + 1].Compare0Interrupt = ChannelN_InterruptM_En[channelNum, timerNum];
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable interrupt of timers 0 & 1 when channel {0} is in {1} mode", 
                            channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.Timer_8bit:
                    if ((timerNum >= 0) && (timerNum <= 3)){
                        ChannelN_InterruptM_En[channelNum, timerNum] = interruptValue;
                        internalTimers[channelNum, timerNum + 3].Compare0Interrupt = ChannelN_InterruptM_En[channelNum, timerNum];
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable interrupt of timers 0 - 3 when channel {0} is in {1} mode", 
                            channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.PWM:
                    //TODO: Set PWM interrupt value
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    //TODO: Set PWM interrupt value
                    if (timerNum == 0){
                        ChannelN_InterruptM_En[channelNum, timerNum] = interruptValue;
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable interrupt of timer0 when channel {0} is in {1} mode", 
                            channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    //TODO: Set PWM interrupt value
                    if ((timerNum == 0) || (timerNum == 1)){
                        ChannelN_InterruptM_En[channelNum, timerNum] = interruptValue;
                    }
                    else{
                        this.Log(LogLevel.Error, "Cannot enable interrupt of timers 0 & 1 when channel {0} is in {1} mode", 
                            channelNum, ChannelN_Control_ChMode[channelNum]);
                    }
                    break;
            }
            this.InfoLog("setting ch{0} timer{1}'s interrupt value to {2} with channel mode {3}", 
                channelNum, timerNum, interruptValue, (ChannelMode)ChannelN_Control_ChMode[channelNum]);
        }

        private bool InterruptEnableReturn(int channelNum, int timerNum){
            bool returnVal = false;
            switch (ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    ChannelN_InterruptM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum].Compare0Interrupt;
                    returnVal = ChannelN_InterruptM_En[channelNum, timerNum];
                    break;
                case ChannelMode.Timer_16bit:
                    ChannelN_InterruptM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 1].Compare0Interrupt;
                    returnVal = ChannelN_InterruptM_En[channelNum, timerNum];
                    break;
                case ChannelMode.Timer_8bit:
                    ChannelN_InterruptM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 3].Compare0Interrupt;
                    returnVal = ChannelN_InterruptM_En[channelNum, timerNum];
                    break;
                case ChannelMode.PWM:
                    //TODO: Set PWM interrupt value
                    returnVal = false;
                    break;
                case ChannelMode.PWM_Timer_16bit:
                    //TODO: Set PWM interrupt value
                    ChannelN_InterruptM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum].Compare0Interrupt;
                    returnVal = ChannelN_InterruptM_En[channelNum, timerNum];
                    break;
                case ChannelMode.PWM_Timer_8bit:
                    //TODO: Set PWM interrupt value
                    ChannelN_InterruptM_En[channelNum, timerNum] = internalTimers[channelNum, timerNum + 1].Compare0Interrupt;
                    returnVal = ChannelN_InterruptM_En[channelNum, timerNum];
                    break;
                default:
                    this.Log(LogLevel.Error, "Error reading ch{0} timer{1}'s interrupt enable. Illegal ChannelMode: {3}", channelNum, timerNum, ChannelN_Control_ChMode[channelNum]);
                    returnVal = false;
                    break;
            }
            return returnVal;
        }

        private void InterruptStatus(int channelNum, int timerNum, bool value){
            //value is inverted by W1C control in register before function call
            Console.WriteLine("intstatus() value: {0}" , value);
            if (!value){
                switch (ChannelN_Control_ChMode[channelNum]){
                    case ChannelMode.Timer_32bit:
                        if (timerNum == 0) {
                            ChannelN_InterruptM_St[channelNum, timerNum]  = value;
                            internalTimers[channelNum, timerNum].Compare0Event = ChannelN_InterruptM_St[channelNum, timerNum]; 
                        }
                        break;
                    case ChannelMode.Timer_16bit:
                        if ((timerNum == 0) || (timerNum == 1)){
                            ChannelN_InterruptM_St[channelNum, timerNum]  = value;
                            internalTimers[channelNum, timerNum + 1].Compare0Event = ChannelN_InterruptM_St[channelNum, timerNum]; 
                        }
                        break;

                    case ChannelMode.Timer_8bit:
                        if ((timerNum >= 0) && (timerNum <= 3)){
                            ChannelN_InterruptM_St[channelNum, timerNum]  = value;
                            internalTimers[channelNum, timerNum + 3].Compare0Event = ChannelN_InterruptM_St[channelNum, timerNum]; 
                        }
                        break;
                }
            }

        }

        private bool InterruptStatusReturn(int channelNum, int timerNum){
            bool returnvalue = false;
            switch (ChannelN_Control_ChMode[channelNum]){
                case ChannelMode.Timer_32bit:
                    if (timerNum == 0) {
                        this.InfoLog("internalTimers[{0}, {1}].Compare0Event = {2}", channelNum, timerNum, internalTimers[channelNum, timerNum].Compare0Event);
                        ChannelN_InterruptM_St[channelNum, timerNum] = internalTimers[channelNum, timerNum].Compare0Event;
                        returnvalue = ChannelN_InterruptM_St[channelNum, timerNum];
                    }
                    break;
                case ChannelMode.Timer_16bit:
                    if ((timerNum == 0) || (timerNum == 1)){
                        ChannelN_InterruptM_St[channelNum, timerNum] = internalTimers[channelNum, timerNum + 1].Compare0Event;
                        returnvalue = ChannelN_InterruptM_St[channelNum, timerNum];
                    }
                    break;

                case ChannelMode.Timer_8bit:
                    if ((timerNum >= 0) && (timerNum <= 3)){
                        ChannelN_InterruptM_St[channelNum, timerNum] = internalTimers[channelNum, timerNum + 3].Compare0Event;
                        returnvalue = ChannelN_InterruptM_St[channelNum, timerNum];
                    }
                    break;
            }
            this.InfoLog("status of ch{0} timer{1}'s is {2} with channel mode {3}",
                channelNum, timerNum, ChannelN_InterruptM_St[channelNum, timerNum], (ChannelMode)ChannelN_Control_ChMode[channelNum]);
            return returnvalue;
        }

        //define registers, read/write callback, bitfields
        private void DefineRegisters()
        {
            //Read Only Register
            Registers.Cfg.Define(this)
                .WithReservedBits(3,29)
                .WithValueField(0, 3, FieldMode.Read, name:"NumCh",
                    valueProviderCallback: _ => channelCount)
            ;

            //Interrupt Enable Register - 0x14
            Registers.IntEn.Define(this)
                .WithReservedBits(16,16)
                .WithFlag(15, FieldMode.Read | FieldMode.Write, name: "Ch3Int3En",
                    changeCallback: (_, value) => InterruptEnable(3, 3, (bool)value), 
                    valueProviderCallback: _   => { return InterruptEnableReturn(3, 3); } )
                .WithFlag(14, FieldMode.Read | FieldMode.Write, name: "Ch3Int2En",
                    changeCallback: (_, value) => InterruptEnable(3, 2, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(3, 2); } )
                .WithFlag(13, FieldMode.Read | FieldMode.Write, name: "Ch3Int1En",
                    changeCallback: (_, value) => InterruptEnable(3, 1, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(3, 1); } )
                .WithFlag(12, FieldMode.Read | FieldMode.Write, name: "Ch3Int0En",
                    changeCallback: (_, value) => InterruptEnable(3, 0, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(3, 0); } )

                .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "Ch2Int3En",
                    changeCallback: (_, value) => InterruptEnable(2, 3, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(2, 3); } )
                .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "Ch2Int2En",
                    changeCallback: (_, value) => InterruptEnable(2, 2, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(2, 2); } )
                .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "Ch2Int1En",
                    changeCallback: (_, value) => InterruptEnable(2, 1, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(2, 1); } )
                .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "Ch2Int0En",
                    changeCallback: (_, value) => InterruptEnable(2, 0, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(2, 0);} )

                .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "Ch1Int3En",
                    changeCallback: (_, value) => InterruptEnable(1, 3, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(1, 3); } )
                .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "Ch1Int2En",
                    changeCallback: (_, value) => InterruptEnable(1, 2, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(1, 2); } )
                .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "Ch1Int1En",
                    changeCallback: (_, value) => InterruptEnable(1, 1, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(1, 1); } )
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch1Int0En",
                    changeCallback: (_, value) => InterruptEnable(1, 0, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(1, 0); } )

                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch0Int3En",
                    changeCallback: (_, value) => InterruptEnable(0, 3, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(0, 3); } )
                .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "Ch0Int2En",
                    changeCallback: (_, value) => InterruptEnable(0, 2, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(0, 2); } )
                .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "Ch0Int1En",
                    changeCallback: (_, value) => InterruptEnable(0, 1, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(0, 1); } )
                .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "Ch0Int0En",
                    changeCallback: (_, value) => InterruptEnable(0, 0, (bool)value),
                    valueProviderCallback: _   => { return InterruptEnableReturn(0, 0); } )
            ;

            //Interrupt Status Register
            Registers.IntSt.Define(this)
                // note: Write 1 to clear, use FieldMode.WriteOneToClear
                //implement R/W here using ChannelN_InterruptM_St[][]
                .WithReservedBits(16, 16)
                .WithFlag(15, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch3Int3",
                    changeCallback: (_, value) => InterruptStatus(3, 3, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(3, 3); })
                .WithFlag(14, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch3Int2",
                    changeCallback: (_, value) => InterruptStatus(3, 2, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(3, 2); })
                .WithFlag(13, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch3Int1",
                    changeCallback: (_, value) => InterruptStatus(3, 1, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(3, 1); })
               .WithFlag(12, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch3Int0",
                    changeCallback: (_, value) => InterruptStatus(3, 0, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(3, 0); })


                .WithFlag(11, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch2Int3",
                    changeCallback: (_, value) => InterruptStatus(2, 3, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(2, 3); })
                .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch2Int2",
                    changeCallback: (_, value) => InterruptStatus(2, 2, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(2, 2); })
                .WithFlag(9, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch2Int1",
                    changeCallback: (_, value) => InterruptStatus(2, 1, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(2, 1); })
                .WithFlag(8, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch2Int0",
                    changeCallback: (_, value) => InterruptStatus(2, 0, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(2, 0); })

                .WithFlag(7, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch1Int3",
                    changeCallback: (_, value) => InterruptStatus(1, 3, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(1, 3); })
                .WithFlag(6, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch1Int2",
                    changeCallback: (_, value) => InterruptStatus(1, 2, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(1, 2); })
                .WithFlag(5, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch1Int1",
                    changeCallback: (_, value) => InterruptStatus(1, 1, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(1, 1); })
                .WithFlag(4, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch1Int0",
                    changeCallback: (_, value) => InterruptStatus(1, 0, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(1, 0); })

                .WithFlag(3, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch0Int3",
                    changeCallback: (_, value) => InterruptStatus(0, 3, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(0, 3); })
                .WithFlag(2, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch0Int2",
                    changeCallback: (_, value) => InterruptStatus(0, 2, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(0, 2); })
                .WithFlag(1, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch0Int1",
                    changeCallback: (_, value) => InterruptStatus(0, 1, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(0, 1); })
                .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear, name: "Ch0Int0",
                    changeCallback: (_, value) => InterruptStatus(0, 0, value),
                    valueProviderCallback: _ => { return InterruptStatusReturn(0, 0); })
            ;

            //Channel/Timer Enable Register - 0x1C
            Registers.ChEn.Define(this)
                .WithReservedBits(16,16)
                .WithFlag(15, FieldMode.Read | FieldMode.Write, name: "Ch3TMR3En/CH3PWMEn",
                    changeCallback: (_, value) =>   { TimerEnable(3, 3, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(3, 3); } )
                .WithFlag(14, FieldMode.Read | FieldMode.Write, name: "Ch3TMR2En",
                    changeCallback: (_, value) =>   { TimerEnable(3, 2, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(3, 2); } )
                .WithFlag(13, FieldMode.Read | FieldMode.Write, name: "Ch3TMR1En",
                    changeCallback: (_, value) =>   { TimerEnable(3, 1, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(3, 1); } )
                .WithFlag(12, FieldMode.Read | FieldMode.Write, name: "Ch3TMR0En",
                    changeCallback: (_, value) =>   { TimerEnable(3, 0, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(3, 0); } )

                .WithFlag(11, FieldMode.Read | FieldMode.Write, name: "Ch2TMR3En/CH2PWMEn",
                    changeCallback: (_, value) =>   { TimerEnable(2, 3, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(2, 3); } )
                .WithFlag(10, FieldMode.Read | FieldMode.Write, name: "Ch2TMR2En",
                    changeCallback: (_, value) =>   { TimerEnable(2, 2, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(2, 2); } )
                .WithFlag(9, FieldMode.Read | FieldMode.Write, name: "Ch2TMR1En",
                    changeCallback: (_, value) =>   { TimerEnable(2, 1, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(2, 1); } )
                .WithFlag(8, FieldMode.Read | FieldMode.Write, name: "Ch2TMR0En",
                    changeCallback: (_, value) =>   { TimerEnable(2, 0, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(2, 0); } )

                .WithFlag(7, FieldMode.Read | FieldMode.Write, name: "Ch1TMR3En/CH1PWMEn",
                    changeCallback: (_, value) =>   { TimerEnable(1, 3, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(1, 3); } )
                .WithFlag(6, FieldMode.Read | FieldMode.Write, name: "Ch1TMR2En",
                    changeCallback: (_, value) =>   { TimerEnable(1, 2, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(1, 2); } )
                .WithFlag(5, FieldMode.Read | FieldMode.Write, name: "Ch1TMR1En",
                    changeCallback: (_, value) =>   { TimerEnable(1, 1, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(1, 1); } )
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch1TMR0En",
                    changeCallback: (_, value) =>   { TimerEnable(1, 0, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(1, 0); } )

                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch0TMR3En/CH0PWMEn",
                    changeCallback: (_, value) =>   { TimerEnable(0, 3, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(0, 3); } )
                .WithFlag(2, FieldMode.Read | FieldMode.Write, name: "Ch0TMR2En",
                    changeCallback: (_, value) =>   { TimerEnable(0, 2, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(0, 2); } )
                .WithFlag(1, FieldMode.Read | FieldMode.Write, name: "Ch0TMR1En",
                    changeCallback: (_, value) =>   { TimerEnable(0, 1, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(0, 1); } )
                .WithFlag(0, FieldMode.Read | FieldMode.Write, name: "Ch0TMR0En",
                    changeCallback: (_, value) =>   { TimerEnable(0, 0, (bool)value); },
                    valueProviderCallback: _   =>   { return TimerEnableReturn(0, 0); } )
            ;

            //Channel 0 Control Register
            Registers.Ch0Ctrl.Define(this) //Channel 0 Control Register
                .WithReservedBits(5,27)
                .WithFlag(4, FieldMode.Read | FieldMode.Write, name: "Ch0PwmPark",
                    changeCallback: (_, value) => { ChannelN_Control_PWM_Park[0] = (bool)value; },
                    valueProviderCallback: _ => { return ChannelN_Control_PWM_Park[0]; } )

                .WithFlag(3, FieldMode.Read | FieldMode.Write, name: "Ch0clk",
                    changeCallback: (_, value) => { ChannelN_Control_ChClk[0] = (bool)value; },
                    valueProviderCallback: _ => { return ChannelN_Control_ChClk[0]; } )

                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch0Mode", 
                    changeCallback: (_, value) => 
                    { 
                        if (Enum.IsDefined(typeof(ChannelMode), (ushort)value))
                        {
                            ChannelN_Control_ChMode[0] = (ChannelMode)(ushort)value;
                            this.InfoLog("Setting channel 0 mode to {0}", (ChannelMode)(ushort)value);
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "Channel 0: unknown channel mode");
                        }
                    },
                    valueProviderCallback: _ => { return (ulong)ChannelN_Control_ChMode[0]; } )
            ;

            //Channel 0 Reload Register
            Registers.Ch0Reload.Define(this)
                .WithValueField(0, 31, FieldMode.Read | FieldMode.Write, name: "TMR32_0", 
                    changeCallback: (_, newValue) => ReloadRegister(0, newValue),
                    valueProviderCallback: _ => { return ReloadRegisterReturn(0); } )
            ;                

            //Channel 0 Counter Register
            Registers.Ch0Cntr.Define(this) //Channel 0 Counter Register
                .WithValueField(0, 31, FieldMode.Read, name: "TMR32_0", 
                valueProviderCallback: _ => { return CounterRegisterReturn(0); } )
            ;

            //Channel 1 Control Register
            Registers.Ch1Ctrl.Define(this) 

            ;

            //Channel 1 Reload
            Registers.Ch1Reload.Define(this)

            ;  

            //Channel 1 Counter Register
            Registers.Ch1Cntr.Define(this) 
                
            ;

            //Channel 2 Control Register
            Registers.Ch2Ctrl.Define(this)
             
            ;

            //Channel 2 Reload Register
            Registers.Ch2Reload.Define(this) 
                
            ;

            //Channel 2 Counter Register
            Registers.Ch2Cntr.Define(this)
                
            ;

            //Channel 3 Control Register
            Registers.Ch3Ctrl.Define(this)
            
            ;

            //Channel 3 Reload Register
            Registers.Ch3Reload.Define(this)
                
            ;

            //Channel 3 Counter Register
            Registers.Ch3Cntr.Define(this)
                
            ;
        }

        private readonly InternalTimer[ , ] internalTimers;

        private const int channelCount = 4;
        private const int TimersPerChannel = 4;
        private const int InternalTimersPerChannel = 7; //7 timer per ch - 1 32 bit, 2 16 bit, 4 8 bit
        private const long timerFrequency = 266000000; //266 MHz
        public long Size => 0x62;

        //register values variables
        private bool [ , ] ChannelN_InterruptM_En = new bool[channelCount, TimersPerChannel];             //ChannelN_InterruptM_En [0][1] is channel 0 interrupt 1 enable
        private readonly bool [ , ] ChannelN_InterruptM_St = new bool[channelCount, TimersPerChannel];    //ChannelN_InterruptM_St [0][1] is channel 0 interrupt 1 status
        private bool [ , ] ChannelN_TimerM_En = new bool[channelCount, TimersPerChannel];                 //ChannelN_TimerM_En [0][1] is channel 0 timer 1 enable

        private bool [] ChannelN_Control_PWM_Park = new bool[channelCount];                //Channel N's PWM park value
        private bool [] ChannelN_Control_ChClk = new bool[channelCount];                   //Channel N's clock source (0 = External clock, 1 = APB Clock)
        private ChannelMode [] ChannelN_Control_ChMode = new ChannelMode[channelCount];    //Channel N's channel mode

        private uint [ , ] ChannelN_Reload = new uint[channelCount, TimersPerChannel];     //Channel N's reload value(s), depends on ChannelN_Control_ChMode

        private uint [] ChannelN_Counter = new uint[channelCount];                         //Channel N's Counter value(s), depends on ChannelN_Control_ChMode

        private class InternalTimer
        {
            public InternalTimer(IPeripheral parent, IClockSource clockSource, int chnum, int index, ulong limit)
            {
                compare0Timer = new ComparingTimer(clockSource, timerFrequency, parent, $"channel{chnum}timer{index}cmp0", limit: limit, compare: limit, enabled: false, workMode: WorkMode.OneShot);

                compare0Timer.CompareReached += () =>
                {
                    Compare0Event = true;
                    //Console.WriteLine("InternalTimer channel {0} timer {1} Compare0Event reached with value {0}", chnum, index, Compare0Event);
                    CompareReached();
                };

                OneShot = true;
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
                    Console.WriteLine("Setting InternalTimer enabled to {0}", value);
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

            public bool Compare0Event { 
                get; 
                set; 
            }

            public bool Compare0Interrupt
            {
                get => compare0Timer.EventEnabled;
                set => compare0Timer.EventEnabled = value;
            }

            public Action OnCompare;

            private void CompareReached()
            {
                OnCompare?.Invoke();

                //Console.WriteLine("ATCPIT100.cs: CompareReached: OneShot value {0}\n", OneShot);

                if(OneShot)
                {
                    Value = 0;
                    //Console.WriteLine("InternalTimer compareReached\n");
                }
            }

            private readonly ComparingTimer compare0Timer;
        }

        private enum ChannelMode : ushort 
        {
            //reserverd     = 0
            Timer_32bit     = 1, // one 32 bit timer 0
            Timer_16bit     = 2, // two 16 bit timers 0 - 1
            Timer_8bit      = 3, // four 8 bit timers 0 - 3
            //reserverd     = 5
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
