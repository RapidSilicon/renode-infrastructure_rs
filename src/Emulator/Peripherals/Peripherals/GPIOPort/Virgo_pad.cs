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
         //InternalRegisters();
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
              	if(EN_S[0]) {
                	switch(MUX_S[0]) {
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
                	if(EN_S[1]) {
                  	switch(MUX_S[1]) {
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
                	if(EN_S[2]) {
                  	switch(MUX_S[2]) {
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
                	if(EN_S[3]) {
                  	switch(MUX_S[3]) {
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
                	if(EN_S[4]) {
                  	switch(MUX_S[4]) {
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
                	if(EN_S[5]) {
                  	switch(MUX_S[5]) {
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
                	if(EN_S[6]) {
                  	switch(MUX_S[6]) {
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
                	if(EN_S[7]) {
                  	switch(MUX_S[7]) {
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
                	if(EN_S[8]) {
                  	switch(MUX_S[8]) {
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
                	if(EN_S[9]) {
                  	switch(MUX_S[10]) {
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
                	if(EN_S[10]) {
                  	switch(MUX_S[10]) {
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
                	if(EN_S[11]) {
                  	switch(MUX_S[11]) {
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
                	if(EN_S[12]) {
                  	switch(MUX_S[12]) {
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
                	if(EN_S[13]) {
                  	switch(MUX_S[13]) {
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
                	if(EN_S[14]) {
                  	switch(MUX_S[14]) {
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
                	if(EN_S[15]) {
                  	switch(MUX_S[15]) {
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
                	if(EN_S[0] && (MUX_S[0] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 17:
                	if(EN_S[1] && (MUX_S[1] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 26:
                	if(EN_S[10] && (MUX_S[10] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 27:
                	if(EN_S[11] && (MUX_S[11] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 28:
                	if(EN_S[12] && (MUX_S[12] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 29:
                	if(EN_S[13] && (MUX_S[13] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 30:
                	if(EN_S[14] && (MUX_S[14] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;
                
                case 31:
                	if(EN_S[15] && (MUX_S[15] == 1)) {
                  	OnPinStateChanged(number-16, value);
                  }
                  break;

                case 32:
                	if(EN_S[0] && (MUX_S[0] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 33:
                	if(EN_S[1] && (MUX_S[1] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 34:
                	if(EN_S[2] && (MUX_S[2] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 35:
                	if(EN_S[3] && (MUX_S[3] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 36:
                	if(EN_S[4] && (MUX_S[4] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 37:
                	if(EN_S[5] && (MUX_S[5] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
            
                case 38:
                	if(EN_S[6] && (MUX_S[6] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 39:
                	if(EN_S[7] && (MUX_S[7] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 40:
                	if(EN_S[8] && (MUX_S[8] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 41:
                	if(EN_S[9] && (MUX_S[9] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 42:
                	if(EN_S[10] && (MUX_S[10] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 43:
                	if(EN_S[11] && (MUX_S[11] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 44:
                	if(EN_S[12] && (MUX_S[12] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 45:
                	if(EN_S[13] && (MUX_S[13] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 46:
                	if(EN_S[14] && (MUX_S[14] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;

                case 47:
                	if(EN_S[15] && (MUX_S[15] == 2)) {
                  	OnPinStateChanged(number-32, value);
                  }
                  break;
                
                case 48:
                	if(EN_S[0] && (MUX_S[0] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 49:
                	if(EN_S[1] && (MUX_S[1] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 58:
                	if(EN_S[10] && (MUX_S[10] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 59:
                	if(EN_S[11] && (MUX_S[11] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;
                
                case 60:
                	if(EN_S[12] && (MUX_S[12] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 61:
                	if(EN_S[13] && (MUX_S[13] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 62:
                	if(EN_S[14] && (MUX_S[14] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 63:
                	if(EN_S[15] && (MUX_S[15] == 3)) {
                  	OnPinStateChanged(number-48, value);
                  }
                  break;

                case 64:
                	if(EN_S[0] && (MUX_S[0] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 65:
                	if(EN_S[0] && (MUX_S[1] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                 
                case 68:
                	if(EN_S[4] && (MUX_S[4] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 69:
                	if(EN_S[5] && (MUX_S[5] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 70:
                	if(EN_S[6] && (MUX_S[6] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 71:
                	if(EN_S[7] && (MUX_S[7] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;
                
                case 72:
                	if(EN_S[8] && (MUX_S[8] == 4)) {
                  	OnPinStateChanged(number-64, value);
                  }
                  break;

                case 73:
                	if(EN_S[9] && (MUX_S[9] == 4)) {
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
               // .WithTaggedFlag("STATUS",0)
                .WithValueField(0, 31,FieldMode.Read|FieldMode.Write , name: "PADKEY",writeCallback: ( _, value) => {
                  if (value==PadKeyUnlockValue) {
                  registerunlocked=true;
                   {  
                       for (var i=0; i<=15; i++ )
                       {
                           EN_S[i]=EN[i];
                       }
                        for (var i=0; i<=15; i++ )
                       {
                           MUX_S[i]=MUX[i];
                       }
                   
                   }
                  
                  }
                   this.InfoLog(" register unlock{0}", registerunlocked);
                }
                  )
                  .WithTaggedFlag("STATUS",31)
                ; 
                
               Registers.std_pu_PAD_GPIO_A_0_ctl.Define(this)

                
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[0]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,               
                writeCallback: (_, value) =>  {

                    MUX[0]=selection((IOMode)value);
                    
                    },  
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                 
                ;
                
                Registers.std_pu_PAD_GPIO_A_1_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[1]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[1]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

                Registers.std_pu_PAD_GPIO_A_2_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[2]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[2]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  
                Registers.std_pu_PAD_GPIO_A_3_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[3]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[3]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ; 
            Registers.std_pu_PAD_GPIO_A_4_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[4]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[4]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_5_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[5]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[5]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_6_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[6]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[6]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_7_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[7]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[7]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_8_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[8]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[8]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_9_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[9]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[9]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
             Registers.std_pu_PAD_GPIO_A_10_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[10]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2, 
                writeCallback: (_, value) =>  {
                    MUX[10]=selection((IOMode)value);
                    },
                
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  
            Registers.std_pu_PAD_GPIO_A_11_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[11]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,  
                writeCallback: (_, value) =>  {
                    MUX[11]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

            Registers.std_pu_PAD_GPIO_A_12_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[12]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,  
                writeCallback: (_, value) =>  {
                MUX[12]=selection((IOMode)value);
                    },
                
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;  

            Registers.std_pu_PAD_GPIO_A_13_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[13]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[13]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_14_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[14]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[14]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;
            Registers.std_pu_PAD_GPIO_A_15_ctl.Define(this)
                .WithFlag(0, FieldMode.Read|FieldMode.Write,name: "EN", writeCallback: ( _, value) => EN[15]=value)
                .WithTag("DS", 1, 2)
                .WithReservedBits(3, 2)
                .WithTaggedFlag("PUE", 5)
                .WithTaggedFlag("PUD", 6)
                .WithEnumField<DoubleWordRegister, IOMode>(7, 2,     
                writeCallback: (_, value) =>  {
                    MUX[15]=selection((IOMode)value);
                    },
                name: "FUNCMAX")
                .WithReservedBits(9, 23) 
                
            ;

      
              
        }

        public bool pin;

        private const int NumberOfGPIOs = 80;
        private uint padKey;
        public const uint PadKeyUnlockValue = 0x2A6;
        public bool registerunlocked=false;

        private uint[] MUX=new uint[16];
        private bool[] EN = new bool[16];
        private uint[] MUX_S=new uint[16];
        private bool[] EN_S = new bool[16];



         public enum IOMode
        {   MainMode = 0, 
            Fpga_pinMode = 1, 
            AlternativeMode = 2, 
            DebugMode = 3
        }
        private enum ShadowRegisters
        {
          Register0,
          Register1,
          Register2,
          Register3,
          Register4,
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