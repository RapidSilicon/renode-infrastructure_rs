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
//using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class Virgo_pad : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public Virgo_pad(IMachine machine) : base(machine, NumberOfGPIOs)
        {
           // locker = new object();
           // IRQ = new GPIO();
            //irqManager = new GPIOInterruptManager(IRQ, State);
         RegistersCollection = new DoubleWordRegisterCollection(this);
          PadGPIOs = new GPIO();
          
         // iomode = new IOMode[NumberOfGPIOs];
         //PadGPIOs  = new GPIO[NumberOfGPIOs];
        
         /*PadGPIOs[0] = GPIO_A_0;
         PadGPIOs[1] = GPIO_A_1;
         PadGPIOs[2] = GPIO_A_2;
         PadGPIOs[3] = GPIO_A_3;
         PadGPIOs[4] = GPIO_A_4;
         PadGPIOs[5] = GPIO_A_5;
         PadGPIOs[6] = GPIO_A_6;
         PadGPIOs[6] = GPIO_A_7;*/
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
        public GPIO GPIO_A_0 { get; } = new GPIO();
        public GPIO GPIO_A_1 { get; } = new GPIO();
        public GPIO GPIO_A_2 { get; } = new GPIO();
        public GPIO GPIO_A_3 { get; } = new GPIO();
        public GPIO GPIO_A_4 { get; } = new GPIO();
        public GPIO GPIO_A_5 { get; } = new GPIO();
        public GPIO GPIO_A_6 { get; } = new GPIO();
        public GPIO GPIO_A_7 { get; } = new GPIO();
        
        public GPIO PadGPIOs { get; set; }
        public long Size => 0x1000;
        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }
        
        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            base.OnGPIO(number, value);
            this.InfoLog("Setting GPIO number #{0} to value {1}", number, value);
             
            pin = pinstate(value);
            
        
        }
             

        public bool pinstate(bool value){
            if(value)
            return true;
            else 
            return false;
        }

        private void PrepareRegisters()
        {
                Registers.std_pu_PAD_GPIO_A_0_ctl.Define(this)
                .WithTaggedFlag("EN", 0)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,
                writeCallback: (_, value) =>  {

                    selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;      
        }
       

        private void selection(IOMode value)
            {
                
                switch((IOMode)value)
                {
                case IOMode.MainMode:               
                this.InfoLog("mode 1"); 
                 PadGPIOs.Set(pin);
                                   
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
      //  public IOMode ioMode;
      public bool pin;
        private const int NumberOfGPIOs = 16;
         //private GPIO[] PadGPIOs { get; }
         
         public enum IOMode
        {   MainMode = 0, 
            Fpga_pinMode = 1, 
            AlternativeMode = 2, 
            DebugMode = 3
            
            /*MainMode = 0b00, 
            Fpga_pinMode = 0b01, 
            AlternativeMode = 0b10, 
            DebugMode = 0b11*/
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