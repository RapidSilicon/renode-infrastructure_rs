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
using static Antmicro.Renode.Peripherals.GPIOPort.ATCGPIO100;
//using Antmicro.Renode.Peripherals.GPIOPort;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class Virgo_pad : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize,IGPIOReceiver
    {
        public Virgo_pad(IMachine machine) : base(machine, NumberOfGPIOs)
        {
            
         RegistersCollection = new DoubleWordRegisterCollection(this);
         PrepareRegisters();
        }

        public override void Reset()
        {
                base.Reset();
        }

        
        public DoubleWordRegisterCollection RegistersCollection { get; }
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
            this.InfoLog("Setting pad number #{0} to value {1}", number, value);
             
            pin = pinstate(value);
            
            // var currentValue = State[number];  
           switch(number) {
            	case 0:
              	if(EN_0) {
                	switch(MUX_0) {
                  	case 1: 
                    	OnPinStateChanged(number+16, value);
                      break;
                    case 2:
                    	OnPinStateChanged(number+32, value);
                      break;
                    default:
                    	break;
                  }
                }
                break;
                
                case 1:
                	if(EN_1) {
                  	switch(MUX_1) {
                    	case 1:
                      	OnPinStateChanged(number+16, value);
                        break;
                      case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                      default:
                      	break;
                    }
                  }
                  break;

                 case 2:
                	if(EN_2) {
                  	switch(MUX_2) {
                    	case 1:
                      	Console.WriteLine("UART TX ");
                        break;
                      case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                      default:
                      	break;
                    }
                  }
                  break; 

                
                case 16:
                	if(EN_0 && (MUX_0 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 17:
                	if(EN_1 && (MUX_1 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 32:
                	if(EN_0 && (MUX_0 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 33:
                	if(EN_1 && (MUX_1 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                 case 34:
                	if(EN_2 && (MUX_2 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
            
            }
                      
        } 

          private void OnPinStateChanged(int number, bool current)
        {    
            Connections[number].Set(pin);
        }

        public bool pinstate(bool value){
            if(value)
            return true;
            else 
            return false;
        }

           private uint selection(IOMode value)
            {
                
                switch((IOMode)value)
                {
                case IOMode.MainMode:               
                this.InfoLog("mode 1"); 
                //PadGPIOs.Set(pin);     
                return 1;            
                case IOMode.Fpga_pinMode:             
                this.InfoLog("mode 2");
                return 2; 
                case IOMode.AlternativeMode:
                this.InfoLog("mode 3"); 
                return 3;
                case IOMode.DebugMode:
                this.InfoLog("mode 4"); 
                return 4;
                default:
                    this.InfoLog(" Non existitng possible value written as selection lines.");
                return 0;
                }
            }
        private void PrepareRegisters()
        {
                Registers.std_pu_PAD_GPIO_A_0_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_0=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,               
                writeCallback: (_, value) =>  {

                    MUX_0=selection((IOMode)value);
                    },  
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 

                ;
                
                Registers.std_pu_PAD_GPIO_A_1_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_1=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_1=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

                Registers.std_pu_PAD_GPIO_A_2_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_2=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_2=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  
                Registers.std_pu_PAD_GPIO_A_3_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_3=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_3=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ; 
            Registers.std_pu_PAD_GPIO_A_4_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_4=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_4=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_5_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_5=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_5=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_6_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_6=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_6=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_7_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_7=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_7=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_8_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_8=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_8=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_9_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_9=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_9=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
             Registers.std_pu_PAD_GPIO_A_10_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_10=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2, 
                writeCallback: (_, value) =>  {
                    MUX_10=selection((IOMode)value);
                    },
                
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  
            Registers.std_pu_PAD_GPIO_A_11_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_11=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,  
                writeCallback: (_, value) =>  {
                    MUX_11=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

            Registers.std_pu_PAD_GPIO_A_12_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_12=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,  
                writeCallback: (_, value) =>  {
                MUX_12=selection((IOMode)value);
                    },
                
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

            Registers.std_pu_PAD_GPIO_A_13_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_13=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_13=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_14_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_14=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_14=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_15_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN_15=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX_15=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;

        }
       
       public bool pin;
      
        private uint MUX_0,MUX_1,MUX_2, MUX_3, MUX_4, MUX_5, MUX_6, MUX_7 , MUX_8 , MUX_9 ,MUX_10,MUX_11, MUX_12,MUX_13,MUX_14,MUX_15;
        private bool EN_0,EN_1,EN_2,EN_3,EN_4,EN_5,EN_6,EN_7,EN_8,EN_9,EN_10,EN_11,EN_12,EN_13,EN_14,EN_15;

        private const int NumberOfGPIOs = 48;
        
         
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