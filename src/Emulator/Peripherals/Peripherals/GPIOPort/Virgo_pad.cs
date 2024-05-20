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
            if(padKey.Value != PadKeyUnlockValue)
                {
                    this.Log(LogLevel.Warning, "Tried to change pin configuration register which is locked. PADKEY value: {0:X}", padKey.Value);
                    return;
                }
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
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
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
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
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
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break; 

                case 3:
                	if(EN_3) {
                  	switch(MUX_3) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 4:
                	if(EN_4) {
                  	switch(MUX_4) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 5:
                	if(EN_5) {
                  	switch(MUX_5) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 6:
                	if(EN_6) {
                  	switch(MUX_6) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 7:
                	if(EN_7) {
                  	switch(MUX_7) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;
            
                case 8:
                	if(EN_8) {
                  	switch(MUX_8) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;
                
                case 9:
                	if(EN_9) {
                  	switch(MUX_10) {
                    case 1:
                        break;
                    case 2:
                      	OnPinStateChanged(number+32 , value);
                        break;
                    case 3:
                        break;
                    case 4:
                        OnPinStateChanged(number+64, value);
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 10:
                	if(EN_10) {
                  	switch(MUX_10) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 11:
                	if(EN_11) {
                  	switch(MUX_11) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 12:
                	if(EN_12) {
                  	switch(MUX_12) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;
                
                case 13:
                	if(EN_13) {
                  	switch(MUX_13) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 14:
                	if(EN_14) {
                  	switch(MUX_14) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
                        break;
                    default:
                      	break;
                    }
                  }
                break;

                case 15:
                	if(EN_15) {
                  	switch(MUX_15) {
                    case 1:
                        OnPinStateChanged(number+16, value);
                        break;
                    case 2:
                      	OnPinStateChanged(number+32, value);
                        break;
                    case 3:
                        OnPinStateChanged(number+48, value);
                        break;
                    case 4:
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

                case 26:
                	if(EN_10 && (MUX_10 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 27:
                	if(EN_11 && (MUX_11 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 28:
                	if(EN_12 && (MUX_12 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 29:
                	if(EN_13 && (MUX_13 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 30:
                	if(EN_14 && (MUX_14 == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 31:
                	if(EN_15 && (MUX_15 == 1)) {
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
                
                case 35:
                	if(EN_3 && (MUX_3 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 36:
                	if(EN_4 && (MUX_4 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 37:
                	if(EN_5 && (MUX_5 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
            
                case 38:
                	if(EN_6 && (MUX_6 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 39:
                	if(EN_7 && (MUX_7 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 40:
                	if(EN_8 && (MUX_8 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 41:
                	if(EN_9 && (MUX_9 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 42:
                	if(EN_10 && (MUX_10 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 43:
                	if(EN_11 && (MUX_11 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 44:
                	if(EN_12 && (MUX_12 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 45:
                	if(EN_13 && (MUX_13 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 46:
                	if(EN_14 && (MUX_14 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 47:
                	if(EN_15 && (MUX_15 == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 48:
                	if(EN_0 && (MUX_0 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 49:
                	if(EN_1 && (MUX_1 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 58:
                	if(EN_10 && (MUX_10 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 59:
                	if(EN_11 && (MUX_11 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 60:
                	if(EN_12 && (MUX_12 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 61:
                	if(EN_13 && (MUX_13 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 62:
                	if(EN_14 && (MUX_14 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 63:
                	if(EN_15 && (MUX_15 == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 64:
                	if(EN_0 && (MUX_0 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 65:
                	if(EN_1 && (MUX_1 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                 
                case 68:
                	if(EN_4 && (MUX_4 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 69:
                	if(EN_5 && (MUX_5 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 70:
                	if(EN_6 && (MUX_6 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 71:
                	if(EN_7 && (MUX_7 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 72:
                	if(EN_8 && (MUX_8 == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 73:
                	if(EN_9 && (MUX_9 == 4)) {
                  	OnPinStateChanged(number-64, value);
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
                Registers.pad_csr.Define(this)
                .WithTaggedFlag("STATUS",0)
                .WithValueField(1, 31, out padKey, name: "PADKEY")
                ; 
                
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
        private const int NumberOfGPIOs = 80;
        private IValueRegisterField padKey;
        private const uint PadKeyUnlockValue = 0x2A6;

         public enum IOMode
        {   MainMode = 0, 
            Fpga_pinMode = 1, 
            AlternativeMode = 2, 
            DebugMode = 3
        }
 
        private enum Registers
        {
            pad_csr = 0x1000,
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