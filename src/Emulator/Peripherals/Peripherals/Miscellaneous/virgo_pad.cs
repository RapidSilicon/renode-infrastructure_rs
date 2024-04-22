//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class virgo_pad : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public virgo_pad(IMachine machine) : base(machine, NumberOfGPIOs)
        {
           // locker = new object();
           // IRQ = new GPIO();
            //irqManager = new GPIOInterruptManager(IRQ, State);
         RegistersCollection = new DoubleWordRegisterCollection(this);
          iomode = new iomode[NumberOfPins];
            PrepareRegisters();
        }
       

        public override void Reset()
        {
           // lock(locker)
           // {
                base.Reset();
               // irqManager.Reset();
               // registers.Reset();
               // IRQ.Unset();
           // }
        }
        public DoubleWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; private set; }  
        public long Size => 0x1000;
        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }


        private void PrepareRegisters()
        {
                Registers.std_pu_PAD_GPIO_A_0_ctl.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
              /* .WithValueField(7, 2, FieldMode.Read|FieldMode.Write, name: "FUNCMUX",
                 writeCallback: (_, val) =>  {
                    this.InfoLog("Mux register"); 
                    })*/
                 .WithEnumField<DoubleWordRegister, IOMode>(7, 2,
                        writeCallback: (_, value) =>  selection((int)value),
                        name: "FUNCMAX")
                .WithReservedBits(9, 23) 
            ;      
        }
       

        private void selection(int i)
            {
                
                switch(iomode[i])
                {
                case IOMode.MainMode:
                this.InfoLog("mode 1"); 
                break;
                case IOMode.Fpga_pinMode:
                this.InfoLog("mode 2"); 
                break;
                case IOMode.AlternativeMode:
                this.InfoLog("mode 3"); 
                break;
                case IOMode.DebugMode:
                this.InfoLog("mode 4"); 
                break;
                default:
                    this.InfoLog(" Non existitng possible value written as selection lines.");
                break;
                }
            }
        private readonly IOMode[] iomode;
        private const int NumberOfGPIOs = 16;
         protected enum IOMode 
        {
            MainMode = 0b00, 
            Fpga_pinMode = 0b01, 
            AlternativeMode = 0b10, 
            DebugMode = 0b11
        }
 
        private enum Registers
        {
          //  pad_csr = 0x1000,
            std_pu_PAD_GPIO_A_0_ctl = 0x1004,
            std_pu_PAD_GPIO_A_1_ctl = 0x1008,
            std_pu_PAD_GPIO_A_2_ctl = 0x100C,
            std_pu_PAD_GPIO_A_3_ctl = 0x1010,
            std_pu_PAD_GPIO_A_4_ctl = 0x1014,
            std_pu_PAD_GPIO_A_5_ctl = 0x1018,
            std_pu_PAD_GPIO_A_6_ctl = 0x101C,
            std_pu_PAD_GPIO_A_7_ctl = 0x1020,
            std_pu_PAD_GPIO_A_8_ctl = 0x1024,
            std_pu_PAD_GPIO_A_9_ctl = 0x1028,
            std_pu_PAD_GPIO_A_10_ctl = 0x102C ,
            std_pu_PAD_GPIO_A_11_ctl = 0x1030,
            std_pu_PAD_GPIO_A_12_ctl = 0x1034,
            std_pu_PAD_GPIO_A_13_ctl = 0x1038,
            std_pu_PAD_GPIO_A_14_ctl = 0x103C,
            std_pu_PAD_GPIO_A_15_ctl = 0x1040
        }
    }
}