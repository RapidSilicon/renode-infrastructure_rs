//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class virgo_pad : BasicDoubleWordPeripheral, IKnownSize  // BaseGPIOPort, 
    {
        public virgo_pad(IMachine machine) : base(machine, NumberOfGPIOs)
        {
           // locker = new object();
           // IRQ = new GPIO();
            //irqManager = new GPIOInterruptManager(IRQ, State);

            PrepareRegisters();
        }

     /*   public uint ReadDoubleWord(long offset)
        {
            lock(locker)
            {
                if(offset < 0x400)
                {
                    var mask = BitHelper.GetBits((uint)(offset >> 2) & 0xFF);
                    var bits = BitHelper.GetBits(registers.Read(0));
                    var result = new bool[8];
                    for(var i = 0; i < 8; i++)
                    {
                        if(mask[i])
                        {
                            result[i] = bits[i];
                        }
                    }

                    return BitHelper.GetValueFromBitsArray(result);
                }

                return registers.Read(offset);
            }
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            lock(locker)
            {
                if(offset < 0x400)
                {
                    var mask = BitHelper.GetBits((uint)(offset >> 2) & 0xFF);
                    var bits = BitHelper.GetBits(value);
                    for(var i = 0; i < 8; i++)
                    {
                        if(mask[i])
                        {
                            Connections[i].Set(bits[i]);
                            State[i] = bits[i];
                        }
                    }
                }
                else
                {
                    registers.Write(offset, value);
                }
            }
        }

        public override void OnGPIO(int number, bool value)
        {
            if(number < 0 || number >= NumberOfGPIOs)
            {
                throw new ArgumentOutOfRangeException(string.Format("Gpio #{0} called, but only {1} lines are available", number, NumberOfGPIOs));
            }

            lock(locker)
            {
                base.OnGPIO(number, value);
                irqManager.RefreshInterrupts();
            }
        }*/

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

        public GPIO IRQ { get; private set; }  
        public long Size => 0x1000;

        private void PrepareRegisters()
        {
            registers = new DoubleWordRegisterCollection(this, new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.std_pu_PAD_GPIO_A_0_ctl, new DoubleWordRegister(this)
                
                .WithTaggedFlag("EN", 0)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithValueField(7, 2, FieldMode.Read|FieldMode.Write, name: "FUNCMUX",
                 writeCallback: (_, val) =>  {
                    this.InfoLog("Mux register"); 
                    })
                .WithReservedBits(9, 23) 
                
                 }
                }) ;        
        }

       /* private void CalculateInterruptTypes()
        {
            lock(locker)
            {
                var isBothEdgesSensitive = BitHelper.GetBits((uint)interruptBothEdgeField.Value);
                var isLevelSensitive = BitHelper.GetBits((uint)interruptSenseField.Value);
                var isActiveHighOrRisingEdge = BitHelper.GetBits((uint)interruptEventField.Value);

                for(int i = 0; i < 8; i++)
                {
                    if(isLevelSensitive[i])
                    {
                        irqManager.InterruptType[i] = isActiveHighOrRisingEdge[i]
                                ? GPIOInterruptManager.InterruptTrigger.ActiveHigh
                                : GPIOInterruptManager.InterruptTrigger.ActiveLow;
                    }
                    else
                    {
                        if(isBothEdgesSensitive[i])
                        {
                            irqManager.InterruptType[i] = GPIOInterruptManager.InterruptTrigger.BothEdges;
                        }
                        else
                        {
                            irqManager.InterruptType[i] = isActiveHighOrRisingEdge[i]
                                ? GPIOInterruptManager.InterruptTrigger.RisingEdge
                                : GPIOInterruptManager.InterruptTrigger.FallingEdge;
                        }
                    }
                }
                irqManager.RefreshInterrupts();
            }
        }

        private uint CalculateMaskedInterruptValue()
        {
            var result = new bool[8];
            for(var i = 0; i < 8; i++)
            {
                result[i] = irqManager.ActiveInterrupts.ElementAt(i) && irqManager.InterruptMask[i];
            }
            return BitHelper.GetValueFromBitsArray(result);
        }

        private DoubleWordRegisterCollection registers;
        private readonly GPIOInterruptManager irqManager;
        private readonly object locker;

        private IValueRegisterField interruptSenseField;
        private IValueRegisterField interruptBothEdgeField;
        private IValueRegisterField interruptEventField;*/

        private const int NumberOfGPIOs = 16;

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