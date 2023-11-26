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
using Antmicro.Renode.Peripherals.Miscellaneous;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ATCWDT200 : BasicDoubleWordPeripheral, IKnownSize
    {
        public ATCWDT200( Machine machine ) : base(machine)
        {
           IRQ = new GPIO();

            interruptTimer = new LimitTimer(machine.ClockSource , timerFrequency,this, "interrupt_timer", InitialLimit, eventEnabled: true);
            interruptTimer.LimitReached += () =>
            {
             interruptPending.Value = true;
                UpdateInterrupts();   
            };

            resetTimer = new LimitTimer(machine.ClockSource,timerFrequency, this, "reset_timer", InitialLimit, eventEnabled: true);
            resetTimer.LimitReached += () =>
            {
                if(BeforeReset?.Invoke() ?? false)
                {
                    return;
                }

                systemReset = true;
                machine.RequestReset();
            };

           

            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            interruptTimer.Reset();
            resetTimer.Reset();
            IRQ.Unset();

            resetSequence = ResetSequence.WaitForFirstByte;

            // We are intentionally not clearing systemReset variable
            // as it should persist after watchdog-triggered reset.
        }
       
    private bool unlock_register (ushort unlock_value ){
      bool unlock_status=false;

      if (unlock_value==WP_NUM){
         
         unlock_value = WP_NUM ;
         
        unlock_status = true;
        this.InfoLog("password unlock");
        
      }
      else {
        this.InfoLog("wrong password");
      }
    
     return unlock_status;
    }

        public long Size => 0x400;

        public GPIO IRQ { get; }

        public Func<bool> BeforeReset { get; set; }

        private void UpdateInterrupts()
        {
            
        }

        private void DefineRegisters()
        {
            Registers.Control.Define(this)
                 .WithFlag(0,FieldMode.Read | FieldMode.Write, name: "CTRL.wdt_en",
                    writeCallback: (_, value) =>
                    {
                        interruptTimer.Enabled = value;
                        resetTimer.Enabled = value;
                    })

                .WithFlag(1, FieldMode.Read | FieldMode.Write,name: "CTRL.clk_set",
                    valueProviderCallback: _ => systemReset,
                    writeCallback: (_, value) => systemReset = value)


                    .WithFlag(2,FieldMode.Read | FieldMode.Write, name: "CTRL.int_en",
                    valueProviderCallback: _ => interruptTimer.EventEnabled,
                    writeCallback: (_, value) =>
                    {
                        interruptTimer.EventEnabled = value;
                        UpdateInterrupts();
                    })

                    .WithFlag(3, FieldMode.Read | FieldMode.Write,name: "CTRL.rst_en",
                    valueProviderCallback: _ => resetTimer.EventEnabled,
                    changeCallback: (_, value) => resetTimer.EventEnabled = value)

                   .WithValueField(4, 4, FieldMode.Read | FieldMode.Write, name: "CTRL.int_period",
                    changeCallback: (_, value) =>
                    {
                        interruptTimer.Limit = 1UL << (31 - (int)value);
                    })

                    .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "CTRL.rst_period",
                    changeCallback: (_, value) =>
                    {
                        resetTimer.Limit = 1UL << (31 - (int)value);
                    })
               
                    .WithReservedBits(11, 21)
            ;
          
            var enable=Registers.Write_Enable.Define(this);
             
                 enable
                 
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Write , name: "WEn",
                    changeCallback: (_, value) =>
                    {
                      unlock_register((ushort)value); 

                    })
               .WithReservedBits(16, 16)
             ;

            Registers.Restart.Define(this)
                .WithValueField(0, 16, FieldMode.Read | FieldMode.Write,name: "RST.wdt_rst",
                    writeCallback: (_, value) =>
                    {  
                         
                    if(){

                        if( resetSequence == ResetSequence.WaitForFirstByte )
                        {
                            resetSequence = ResetSequence.WaitForSecondByte;
                            this.InfoLog("Enable write to restart register");
                        }
                        else if(resetSequence == ResetSequence.WaitForSecondByte && value == RESTART_NUM )
                        {
                            resetSequence = ResetSequence.WaitForFirstByte;
                            interruptTimer.Value = interruptTimer.Limit;
                            resetTimer.Value = resetTimer.Limit;
                            this.InfoLog("restart register get unlocked");
                        }
                        else
                        {
                            resetSequence = ResetSequence.WaitForFirstByte;
                            this.InfoLog("restart register is write protected");
                        }
                    }
                    })
                .WithReservedBits(16, 16)
                ;
        
            

            /* Registers.Status.Define(this)
                 .WithFlag(0, name: "IntExpired",
                    valueProviderCallback: _ => ,
                    changeCallback: (_, value) =>   )
              
              .WithReservedBits(1, 31)
             ;*/
             
        }

        private ResetSequence resetSequence;
        private bool systemReset;

        private IFlagRegisterField interruptPending;

        private readonly LimitTimer interruptTimer;
        private readonly LimitTimer resetTimer;

        private const ulong InitialLimit = (1UL << 31);
        

          private const ushort WP_NUM = 0x5AA5;

          private const ushort RESTART_NUM = 0xCAFE;
           
         private const long timerFrequency = 266000000; //266 MHz
        private enum ResetSequence
        {
            WaitForFirstByte,
            WaitForSecondByte
        }

        private enum Registers
        {
            Control = 0x10,
            Restart = 0x14,
            Write_Enable = 0x18,
            Status = 0x1C,
        }
    }
}
