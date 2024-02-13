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
using System.Threading;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Timers
{
  public class ATCWDT200 : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
  {
    public ATCWDT200(Machine machine) : base(machine)
    {
      DefineRegisters();

      IRQ0 = new GPIO();
      IRQ1 = new GPIO();

      interruptTimer = new LimitTimer(machine.ClockSource, timerFrequency, this, "interrupt_timer", InitialLimit1, workMode: WorkMode.OneShot, eventEnabled: true);
      interruptTimer.LimitReached += () =>
      {
        this.InfoLog("Interrupt timer limit reached");
        interruptPending.Value = true;

        UpdateInterrupts();
        resetTimer.Enabled = true;
        this.InfoLog("Enable reset timer");
        resetTimer.Value = resetTimer.Limit;
      };

      resetTimer = new LimitTimer(machine.ClockSource, timerFrequency, this, "reset_timer", InitialLimit2, workMode: WorkMode.OneShot, eventEnabled: true);
      resetTimer.LimitReached += () =>
      {
        IRQ1.Set();
        this.InfoLog("Sending reset signal");
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
      this.InfoLog("Timer reset");
    }

    public void OnGPIO(int number, bool value)
    {
      // check only for gpio pause signal in gpio 0
      if(number == 0){
        this.Log(LogLevel.Info, "WDT Pause signal value changed");
        interruptTimer.Enabled = !value;
        resetTimer.Enabled = !value;
      }
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

    private ulong ResetInterval(int value)
    {
      ulong interval;
      switch (value)
      {
        case 0:
          interval = 1UL << (31 - (int)24);
          break;
        case 1:
          interval = 1UL << (31 - (int)23);
          break;
        case 2:
          interval = 1UL << (31 - (int)22);
          break;
        case 3:
          interval = 1UL << (31 - (int)21);
          break;
        case 4:
          interval = 1UL << (31 - (int)20);
          break;
        case 5:
          interval = 1UL << (31 - (int)19);
          break;
        case 6:
          interval = 1UL << (31 - (int)18);
          break;
        case 7:
          interval = 1UL << (31 - (int)17);
          break;
        default:
          interval = 1UL << (31 - (int)24);
          break;
      }

      return interval;
    }

    private ulong InterruptInterval(int value)
    {
      ulong interval;
      switch (value)
      {
        case 0:
          interval = 1UL << (31 - (int)25);
          break;
        case 1:
          interval = 1UL << (31 - (int)23);
          break;
        case 2:
          interval = 1UL << (31 - (int)21);
          break;
        case 3:
          interval = 1UL << (31 - (int)20);
          break;
        case 4:
          interval = 1UL << (31 - (int)19);
          break;
        case 5:
          interval = 1UL << (31 - (int)18);
          break;
        case 6:
          interval = 1UL << (31 - (int)17);
          break;
        case 7:
          interval = 1UL << (31 - (int)16);
          break;
        case 8:
          interval = 1UL << (31 - (int)14);
          break;
        case 9:
          interval = 1UL << (31 - (int)12);
          break;
        case 10:
          interval = 1UL << (31 - (int)10);
          break;
        case 11:
          interval = 1UL << (31 - (int)8);
          break;
        case 12:
          interval = 1UL << (31 - (int)6);
          break;
        case 13:
          interval = 1UL << (31 - (int)4);
          break;
        case 14:
          interval = 1UL << (31 - (int)2);
          break;
        case 15:
          interval = (1UL << (31));
          break;
        default:
          interval = 1UL << (31 - (int)25);
          break;
      }

      return interval;
    }

    public long Size => 0x400;

    public GPIO IRQ0 { get; set; }

    public GPIO IRQ1 { get; set; }

    private void UpdateInterrupts()
    {
      if (interruptTimer.EventEnabled && interruptPending.Value)
      {
        IRQ0.Set();
        this.InfoLog("Sending interval interrupt signal");
      }
      else
      {
        IRQ0.Unset();
      }
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
                  interruptTimer.Value = interruptTimer.Limit;
                  this.InfoLog("Timer enabled");
                }
              })

          .WithFlag(1, name: "CTRL.clk_set",
             writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {
                  Control_ChClk = (bool)value;
                  if (value)
                  {
                    this.InfoLog("Peripheral clock selected");
                  }
                  else
                  {
                    this.InfoLog("External clock selected");
                  }
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
                  this.InfoLog("Interrupt timer enable");
                }
              })

            .WithFlag(3, name: "CTRL.rst_en",
              valueProviderCallback: _ => resetTimer.EventEnabled,
              writeCallback: (_, value) =>
             {
               if (CheckifUnlock(Registers.Control))
               {
                 resetTimer.EventEnabled = value;
                 this.InfoLog("Reset timer enable");
               }
             })

            .WithValueField(4, 4, name: "CTRL.int_period",
              writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {
                  interruptTimer.Limit = InterruptInterval((int)value);
                  this.InfoLog("Interrupt interval set to {0}", interruptTimer.Limit);
                }
              })

            .WithValueField(8, 3, name: "CTRL.rst_period",
              writeCallback: (_, value) =>
              {
                if (CheckifUnlock(Registers.Control))
                {
                  resetTimer.Limit = ResetInterval((int)value);
                  this.InfoLog("Reset interval set to {0}", resetTimer.Limit);
                }
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
                  this.InfoLog("Write enable for restart register");

                  if (value == RESTART_NUM && resetSequence == ResetSequence.WaitForRestart)
                  {
                    resetSequence = ResetSequence.WaitForUnlock;
                    this.InfoLog("Timer restarted to {0}", interruptTimer.Limit);

                    interruptTimer.Value = interruptTimer.Limit;
                    resetTimer.Value = resetTimer.Limit;
                    resetTimer.Enabled = false;
                    interruptTimer.Enabled = false;
                    interruptTimer.Enabled = true;
                  }
                }
                else
                {
                  this.InfoLog("Restart register is write protected");
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

    private const ushort WP_NUM = 0x5AA5;
    private const ushort RESTART_NUM = 0xCAFE;

    private const ulong InitialLimit1 = (1UL << 31);
    private const ulong InitialLimit2 = (1UL << 14);

    private const long timerFrequency = 133000000; //266 MHz

    private bool Control_ChClk;
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