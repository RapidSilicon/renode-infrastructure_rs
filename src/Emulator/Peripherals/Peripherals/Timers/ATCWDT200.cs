//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
#define ATCWDT200_32BIT_TIMER
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
using System.Threading;

namespace Antmicro.Renode.Peripherals.Timers
{
  public class ATCWDT200 : BasicDoubleWordPeripheral, IKnownSize
  {
    public ATCWDT200(Machine machine) : base(machine)
    {
      DefineRegisters();

      IRQ0 = new GPIO();
      IRQ1 = new GPIO();


      interruptTimer = new LimitTimer(machine.ClockSource, timerFrequency, this, "interrupt_timer", InitialLimit, workMode: WorkMode.OneShot, eventEnabled: true);


      interruptTimer.LimitReached += () =>
      {

        this.InfoLog("interrupt limit reached");
        interruptPending.Value = true;

        UpdateInterrupts();
        resetTimer.Enabled = true;
        this.InfoLog("enable watchdog reset timer {0}", resetTimer.Enabled);




      };





      resetTimer = new LimitTimer(machine.ClockSource, timerFrequency, this, "reset_timer", InitialLimit, workMode: WorkMode.OneShot, eventEnabled: true);



      resetTimer.LimitReached += () =>
           {



             IRQ1.Set();
             this.InfoLog("Sending  reset signal {0}", IRQ1.IsSet);

             this.InfoLog("reset limit reached");

             // machine.RequestReset();

           };

    }

    public override void Reset()
    {
      base.Reset();
      interruptTimer.Reset();
      resetTimer.Reset();
      IRQ0.Unset();
      IRQ1.Unset();

      resetSequence = ResetSequence.WaitForUnlock;

      this.InfoLog("Watchdog timer reset");

    }


    private bool CheckifUnlock(Registers reg)
    {

      if (registersUnlocked)
      {
        return true;

      }

      this.Log(LogLevel.Warning, "Writing to {0} is allowed only when the register is unlocked", reg);

      return false;

    }




    private ulong resetinterval(int value)
    {
      ulong interval;
      switch (value)
      {

        case 0:
          interval = 1UL << (31 - (int)24);
          this.InfoLog("case 1");

          break;
        case 1:
          interval = 1UL << (31 - (int)23);
          this.InfoLog("case 2");
          break;
        case 2:
          interval = 1UL << (31 - (int)22);
          this.InfoLog("case 3");
          break;
        case 3:
          interval = 1UL << (31 - (int)21);
          this.InfoLog("case 4");
          break;
        case 4:
          interval = 1UL << (31 - (int)20);
          this.InfoLog("case 5");
          break;
        case 5:
          interval = 1UL << (31 - (int)19);
          this.InfoLog("case 6");
          break;
        case 6:
          interval = 1UL << (31 - (int)18);
          this.InfoLog("case 7");
          break;
        case 7:
          interval = 1UL << (31 - (int)17);
          this.InfoLog("case 8");
          break;
        default:
          interval = 0;
          this.InfoLog("case default");
          break;
      }

      return interval;


    }


    private ulong interruptinterval(int value)
    {
      ulong interval;
      switch (value)
      {

        case 0:
          interval = 1UL << (31 - (int)25);
          this.InfoLog("case 0");

          break;
        case 1:
          interval = 1UL << (31 - (int)23);
          this.InfoLog("case 1");
          break;
        case 2:
          interval = 1UL << (31 - (int)21);
          this.InfoLog("case 2");
          break;
        case 3:
          interval = 1UL << (31 - (int)20);
          this.InfoLog("case 3");
          break;
        case 4:
          interval = 1UL << (31 - (int)19);
          this.InfoLog("case 4");
          break;
        case 5:
          interval = 1UL << (31 - (int)18);
          this.InfoLog("case 5");
          break;
        case 6:
          interval = 1UL << (31 - (int)17);
          this.InfoLog("case 6");
          break;
        case 7:
          interval = 1UL << (31 - (int)16);
          this.InfoLog("case 7");
          break;

        case 8:
          interval = 1UL << (31 - (int)14);
          this.InfoLog("case 8");
          break;
        case 9:
          interval = 1UL << (31 - (int)12);
          this.InfoLog("case 9");
          break;
        case 10:
          interval = 1UL << (31 - (int)10);
          this.InfoLog("case 10");
          break;
        case 11:
          interval = 1UL << (31 - (int)8);
          this.InfoLog("case 11");
          break;
        case 12:
          interval = 1UL << (31 - (int)6);
          this.InfoLog("case 12");
          break;
        case 13:
          interval = 1UL << (31 - (int)4);
          this.InfoLog("case 13");
          break;
        case 14:
          interval = 1UL << (31 - (int)2);
          this.InfoLog("case 14");
          break;
        case 15:
          interval = (1UL << (31));
          this.InfoLog("case 15");
          break;

        default:
          interval = 0;
          this.InfoLog("case default");
          break;
      }

      return interval;


    }




    public long Size => 0x400;

    public GPIO IRQ0 { get; set; }

    public GPIO IRQ1 { get; set; }
    public Func<bool> BeforeReset { get; }


    private void UpdateInterrupts()
    {
      if (interruptTimer.EventEnabled && interruptPending.Value)
      {
        IRQ0.Set();
        this.InfoLog("Sending  interrupt signal {0}", IRQ0.IsSet);

      }

      else
      {
        IRQ0.Unset();
      }

    }

    private void ResetInnerStatus()
    {
      firstStageUnlocked = false;
      registersUnlocked = false;

    }

    private void DefineRegisters()
    {
      Registers.Control.Define(this)
           .WithFlag(0, name: "CTRL.wdt_en",

              writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))

                {
                  interruptTimer.Enabled = value;

                  this.InfoLog("enable watchdog {0}", interruptTimer.Enabled);

                }

              })

          .WithFlag(1, name: "CTRL.clk_set",

              writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))

                {
                  Control_ChClk = (bool)value;
                  this.InfoLog("Clock select is {0}", Control_ChClk);

                }
              },
              valueProviderCallback: _ => { return Control_ChClk; })


            .WithFlag(2, name: "CTRL.int_en",
              valueProviderCallback: _ => interruptTimer.EventEnabled,
              writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {

                  interruptTimer.EventEnabled = value;
                  this.InfoLog("interrupt enable {0}", interruptTimer.EventEnabled);

                  // UpdateInterrupts(); 
                }
              })

            .WithFlag(3, name: "CTRL.rst_en",
              valueProviderCallback: _ => resetTimer.EventEnabled,
              changeCallback: (_, value) =>
             {
               if (CheckifUnlock(Registers.Control))

               {
                 resetTimer.EventEnabled = value;
                 this.InfoLog("reset enable {0}", resetTimer.EventEnabled);


               }
             })

            .WithValueField(4, 4, name: "CTRL.int_period",
              changeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {
                  interruptTimer.Limit = interruptinterval((int)value);
                  this.InfoLog("interrupt interval set to {0}", interruptTimer.Limit);

                }
              })

            .WithValueField(8, 3, name: "CTRL.rst_period",
              changeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {
                  resetTimer.Limit = resetinterval((int)value);
                  this.InfoLog("reset interval set to {0}", resetTimer.Limit);

                }
                // ResetInnerStatus();
              }
              )

            .WithReservedBits(11, 21)
      ;



      Registers.Restart.Define(this)
          .WithValueField(0, 16, name: "RST.wdt_rst",
              writeCallback: (_, value) =>
              {

                if (CheckifUnlock(Registers.Restart))
                {

                  resetSequence = ResetSequence.WaitForRestart;
                  this.InfoLog("Enable write to restart register");

                  // ResetInnerStatus();

                  if (value == RESTART_NUM && resetSequence == ResetSequence.WaitForRestart)
                  {
                    resetSequence = ResetSequence.WaitForUnlock;
                    this.InfoLog("restart register get restarted {0}, {1}", interruptTimer.Limit, resetTimer.Limit);

                    interruptTimer.Value = interruptTimer.Limit;
                    resetTimer.Value = resetTimer.Limit;
                    resetTimer.Enabled = false;
                    interruptTimer.Enabled = false;
                    interruptTimer.Enabled = true;


                  }
                }
                else
                {


                  this.InfoLog("restart register is write protected");
                  resetSequence = ResetSequence.WaitForUnlock;

                }

              }
              )
          .WithReservedBits(16, 16)
          ;

      Registers.Write_Enable.Define(this)

          .WithValueField(0, 16, name: "WEn",
              changeCallback: (_, value) =>
              {
                if (value == WP_NUM && !firstStageUnlocked)

                {
                  registersUnlocked = true;
                  firstStageUnlocked = true;
                }


              },
              valueProviderCallback: _ => 0)
         .WithReservedBits(16, 16)
       ;

      Registers.Status.Define(this)
            .WithFlag(0, out interruptPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "CTRL.int_flag",
               writeCallback: (_, __) => UpdateInterrupts()


               )

         .WithReservedBits(1, 31)
        ;

    }



    private ResetSequence resetSequence;



    private IFlagRegisterField interruptPending;

    private readonly LimitTimer interruptTimer;
    private readonly LimitTimer resetTimer;

    private const ulong InitialLimit = (1UL << 31);
    private bool Control_ChClk;

    private const ushort WP_NUM = 0x5AA5;

    private const ushort RESTART_NUM = 0xCAFE;

    private const long timerFrequency = 133000000; //266 MHz
    private bool firstStageUnlocked;

    private bool registersUnlocked;
    private enum ResetSequence
    {
      WaitForUnlock,

      WaitForRestart
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