//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.MTD;
using System.Collections.Specialized;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class ATCSPI200 : NullRegistrationPointPeripheralContainer<ISPIPeripheral>, IDoubleWordPeripheral, IKnownSize /*IWordPeripheral, IBytePeripheral ,NullRegistrationPointPeripheralContainer<ISPIFlash>*/
    {
        public ATCSPI200(IMachine machine, uint fifoSize, bool hushTxFifoLevelWarnings = false) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            rxQueue = new Queue<uint>();
            txQueue = new Queue<uint>();
            this.fifoSize=fifoSize;
        }

        public override void Reset()
        { 
            registers.Reset();
            rxQueue.Clear();
            txQueue.Clear();
            transactionInProgress = false;
        }

        public uint ReadDoubleWord(long address)
        {
            return registers.Read(address);
        }

        public void WriteDoubleWord(long address, uint value)
        {
            registers.Write(address, value);
        }

        public long Size => 0x1000;

        public GPIO IRQ { get; }

        public int NumberOfSlaves { get; }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registerMap = new Dictionary<long, DoubleWordRegister>()
            {   {(long)Registers.TransferFormat, new DoubleWordRegister(this)
                    .WithTaggedFlag("CPHA", 0)
                    .WithTaggedFlag("CPOL", 1)
                    .WithTaggedFlag("SlvMode", 2)
                    .WithFlag(3,out leastSignificantBitFirst,name:"LSB")
                    .WithTaggedFlag("MOSIBiDir", 4)
                    .WithReservedBits(5, 2)
                    .WithFlag(7,out dataMerge,FieldMode.Read | FieldMode.Write, name: "DataMerge")
                    .WithValueField(8, 5,out dataLength,FieldMode.Read | FieldMode.Write, name: "DataLen")
                    .WithReservedBits(13, 3)
                    .WithEnumField<DoubleWordRegister, AddressLength>(16, 2,out addressLength,FieldMode.Read | FieldMode.Write, name: "AddrLen")
                    .WithReservedBits(18, 14)
                     
                }, 

                {(long)Registers.TransferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 9,out readCount, FieldMode.Read | FieldMode.Write, name: "RdTranCnt")
                    .WithValueField(9, 2,out dummyCount,FieldMode.Read | FieldMode.Write,name: "DummyCnt")
                    .WithTaggedFlag("TokenValue", 11)
                    .WithValueField(12, 9,out writeCount,FieldMode.Read | FieldMode.Write, name: "WrTranCnt")//,writeCallback: (_,__) =>{this.InfoLog("Write counts are {0}", writeCount.Value+1);})
                    .WithTaggedFlag("TokenEn", 21)
                    .WithEnumField<DoubleWordRegister, DualQuad>(22, 2, out dualQuad, name: "DualQuad")
                    .WithEnumField<DoubleWordRegister, TransferMode>(24, 4, out transferMode, name: "TransMode")
                    .WithFlag(28,out addressformat, FieldMode.Read | FieldMode.Write,name:"AddrFmt")          
                    .WithFlag(29,out addressPhaseEnable, FieldMode.Read | FieldMode.Write, name: "AddrEn")
                    .WithFlag(30,out commandPhaseEnable, FieldMode.Read |FieldMode.Write, name: "CmdEn")
                    .WithTaggedFlag("SlvDataOnly", 31)
                    .WithWriteCallback((_, __) => 
                    {
                        byteCount = (int)dataLength.Value / 8 + 1;
                        bytestoTransfer = ((int)(writeCount.Value+1))* (byteCount);
                        bytesfromslave = ((int)readCount.Value+1) *(byteCount);
                    })
                },

                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithValueField(0, 8,out command, FieldMode.Read |FieldMode.Write,name: "CMD",writeCallback: (_,__) => {transactionInProgress=true;})
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => TrySendData())   
                },

                {(long)Registers.Address, new DoubleWordRegister(this)
                    .WithValueField(0, 32,out serialFlashAddress ,FieldMode.Read |FieldMode.Write, name: "Address")
                },
                
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32,FieldMode.Read |FieldMode.Write, name: "DATA",
                        writeCallback: (_, val) => EnqueueToTransmitBuffer((uint)val),
                       valueProviderCallback: _ =>
                       {   
                       
                            if(!TryDequeueFromReceiveBuffer(out var data))
                            {
                                this.Log(LogLevel.Warning, "Trying to read from an empty FIFO");
                                return 0;
                            }

                            if(readCount.Value+1 <= fifoSize)
                            {
                                --bytesfromslave;
                            }

                            if(readCount.Value+1 > fifoSize && rxQueue.Count==0)
                            {  
                                bytesfromslave=bytesfromslave-(int)fifoSize;
                                for (var i=0; i<=bytesfromslave; i++)
                                {
                                    HandleByteReception();
                                }
    
                            }
                        
                            if ( bytesfromslave==0 )
                            {
                                transactionInProgress = false;
                                RegisteredPeripheral.FinishTransmission();
                                this.InfoLog("Transmission finish after read");
                                rxFull=false;
                                Reset();
                                return data;
                            } 
                            return data;             
                        })    
                },     
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "SPIRST",
                        changeCallback: (_, value) => 
                        {  
                            if(value)
                            {
                                this.Log(LogLevel.Debug, "Software Reset requested by writing RST to the Control Register");
                                Reset();
                            }
                        })
                    .WithFlag(1, FieldMode.Read |FieldMode.Write,name: "RXFIFORST",
                        writeCallback: (_, value) => {if(value) rxQueue.Clear();})
                    .WithFlag(2, FieldMode.Read |FieldMode.Write,name: "TXFIFORST",
                        writeCallback: (_, value) => {if(value) txQueue.Clear();})
                    .WithTaggedFlag("RXDMAEN",3)
                    .WithTaggedFlag("TXDMAEN",4)
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 8, out rxFIFOThreshold, name: "RXTHRES")
                    .WithValueField(16, 5, out txFIFOThreshold, name: "TXTHRES")
                    .WithReservedBits(24, 8)     
                },
                
               {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0,FieldMode.Read, name : "SPIActive",valueProviderCallback: (_) => {return transactionInProgress;})
                    .WithReservedBits(1, 7)
                    .WithValueField(8, 6, name: "RXNUM")
                    .WithFlag(14,FieldMode.Read, name :"RXEMPTY",valueProviderCallback: (_) => rxQueue.Count==0)
                    .WithFlag(15,FieldMode.Read,name :"RXFULL",valueProviderCallback: (_) => 
                    {   
                        if(rxQueue.Count == fifoSize)
                        {
                            rxFull=true;
                        } 
                       return rxFull;
                    })
                    .WithValueField(16, 6, name: "TXNUM")
                    .WithFlag(22,FieldMode.Read, name:"TXEMPTY",valueProviderCallback: (_) => txQueue.Count == 0)
                    .WithFlag(23,FieldMode.Read,name:"TXFULL",valueProviderCallback: (_) =>
                    { 
                        if(txQueue.Count == fifoSize)
                        {
                            txFull=true;
                        }
                      return txFull;
                    })
                    .WithValueField(24, 2, name: "RXNUM1")
                    .WithReservedBits(26, 2)
                    .WithValueField(28, 2, name: "TXNUM1")
                    .WithReservedBits(30, 2)     
                },

                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    .WithValueField(0, 4,FieldMode.Read, name: "RXFIFO - Receive FIFO Size (in words; size = 2^RXFIFO)"
                    ,valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
                    )
                    .WithValueField(4, 4, FieldMode.Read, name: "TXFIFO - Transmit FIFO Size (in words; size = 2^TXFIFO)"
                    ,valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
                    )
                    .WithTaggedFlag("DUALSPI", 8)
                    .WithTaggedFlag("QUADSPI", 9)
                    .WithReservedBits(10, 1)
                    .WithTaggedFlag("DirectIO", 11)
                    .WithTaggedFlag("AHBMem", 12)
                    .WithTaggedFlag("EILMMem", 13)
                    .WithTaggedFlag("SLAVE", 14)
                    .WithReservedBits(15, 17)       
                }
            };

            return registerMap;
        }

        
        public bool TryDequeueFromReceiveBuffer(out uint data)
        {
            if(!rxQueue.TryDequeue(out data))
            {
                data = 0;
                return false;
            }
            return true;
        }

        private void EnqueueToTransmitBuffer(uint val)
        {
            if(txQueue.Count == fifoSize)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full FIFO. Dropping the data");
                return;
            }

            txQueue.Enqueue(val);
            //smaller transaction condition  and for larget transaction , SPIActive and buffer is full and then transaction carried out
            if(txQueue.Count == (int)writeCount.Value+1 || (transactionInProgress && txQueue.Count == fifoSize) )
            {
                HandleByteTransmission();
            }
        }
        
        private void PerformTransaction(int size,bool readFromFifo,bool writeToFifo)
        {  
       
            if(RegisteredPeripheral == null)
            {
                this.ErrorLog("Attempted to perform a UMA transaction with no peripheral attached");
                return;
            }
           
            if(commandPhaseEnable.Value)
            {
                RegisteredPeripheral.Transmit((byte)command.Value);
            }
           
            if(addressPhaseEnable.Value)
            {
                var a0 = (byte)(serialFlashAddress.Value);
                var a1 = (byte)(serialFlashAddress.Value>>8);
                var a2 = (byte)(serialFlashAddress.Value>>16);
                RegisteredPeripheral.Transmit(a2);
                RegisteredPeripheral.Transmit(a1);
                RegisteredPeripheral.Transmit(a0);
            }

            if (readFromFifo)
            {
          
                this.InfoLog("write");
            }

            else if(writeToFifo)
            {
                for (var i=0; i<bytesfromslave; i++)
                {
                    HandleByteReception();
                }
            this.InfoLog("read");
            }

            TryFinishTransmission();                    
        }


         private void TrySendData(){
            switch(transferMode.Value)
            {
                case TransferMode.WriteRead:
                    PerformTransaction(txQueue.Count, readFromFifo: true, writeToFifo: true);
                    break;
                case TransferMode.Write:
                    if (command.Value == 0x02)
                    {
                    PerformTransaction(txQueue.Count,readFromFifo: true, writeToFifo: false);
                    }
                     
                    if(command.Value==0xD8 || command.Value==0xC7)
                    {
                    this.InfoLog("Erase");
                    }
                    break;
                case TransferMode.Read: 
                    PerformTransaction(rxQueue.Count,readFromFifo: false, writeToFifo: true);
                    break;
                case TransferMode.WriteandRead:
                   break; 
                case TransferMode.ReadandWrite:
                   break;
                case TransferMode.WriteDummyRead:
                   break;
                case TransferMode.ReadDummyWrite:
                   break;
                case TransferMode.NoneData:
                    PerformTransaction(txQueue.Count,readFromFifo: false, writeToFifo: false);                       
                    break;
                case TransferMode.DummyWrite:
                    break;
                case TransferMode.DummyRead:
                    break;
                default:
                    break;
            }

        }

        private void HandleByteTransmission()
        {
            var bytes = new byte[MaxPacketBytes];
            var reverseBytes = BitConverter.IsLittleEndian; 
            bytestoTransfer = bytestoTransfer - (int)txQueue.Count;

            while(txQueue.Count!=0)
            {
                var value = txQueue.Dequeue();
                BitHelper.GetBytesFromValue(bytes, 0, value, byteCount, reverseBytes);
                for(var i = 0; i < byteCount; i++)
                {   
                    bytes[i] = RegisteredPeripheral.Transmit(bytes[i]);
                }
            }
            txQueue.Clear();

            if(bytestoTransfer!=0)
            {
            return;
            }
            transactionInProgress=false;
            RegisteredPeripheral.FinishTransmission();
            this.InfoLog("Transmission finish after write");
            Reset();
        }
        
        private void HandleByteReception()
        {    
           if(rxQueue.Count == fifoSize) 
           {
                return;
           }
            var receivedByte = RegisteredPeripheral.Transmit(0);
            rxQueue.Enqueue(receivedByte);       
        }

        private void TryFinishTransmission()
        {   
            if(transferMode.Value == TransferMode.Write || transferMode.Value == TransferMode.Read)
            {
                return;
            }
            transactionInProgress = false;
            RegisteredPeripheral.FinishTransmission();
            Reset();
        }

       
        private IValueRegisterField dataLength;  
        private IValueRegisterField command; 
        private IValueRegisterField serialFlashAddress; 
        
        private IValueRegisterField rxFIFOThreshold;
        private IValueRegisterField txFIFOThreshold;
        private IValueRegisterField writeCount;
        private IValueRegisterField readCount;
        private IValueRegisterField dummyCount;
    
        private IFlagRegisterField commandPhaseEnable; 
        private IFlagRegisterField addressPhaseEnable;
        private IFlagRegisterField addressformat;
        private IFlagRegisterField leastSignificantBitFirst;
        private IFlagRegisterField dataMerge;

        private IEnumRegisterField<AddressLength> addressLength;
        private IEnumRegisterField<TransferMode> transferMode;
        private IEnumRegisterField<DualQuad> dualQuad;

        private readonly uint fifoSize;   
        private const int MaxPacketBytes = 4;
        private bool transactionInProgress;
        private bool txFull = false;
        private bool rxFull ;
        private int bytestoTransfer;
        private int bytesfromslave;
        public int byteCount;
        
        private readonly Queue<uint> rxQueue;  
        private readonly Queue<uint> txQueue;  
        private readonly DoubleWordRegisterCollection registers;

        private enum AddressLength
        { 
            oneByte = 0,
            twoByte = 1,
            threeByte = 2,
            fourByte = 3
        }

        private enum TransferMode
        {
            WriteRead = 0,
            Write = 1,
            Read = 2,
            WriteandRead = 3,
            ReadandWrite = 4,
            WriteDummyRead = 5,
            ReadDummyWrite = 6,
            NoneData = 7,
            DummyWrite = 8,
            DummyRead =9 
        }
      
        private enum DualQuad
        {
            Regular = 0,
            Dual = 1,
            Quad = 2,
            Reserved = 3
        }

        private enum Registers : long
        {
            TransferFormat = 0x10,
            DirectIOControl = 0x14,
            TransferControl = 0x20,
            Command = 0x24,
            Address = 0x28,
            Data = 0x2C,
            Control = 0x30,
            Status = 0x34,
            InterruptEnable = 0x38,
            InterruptStatus = 0x3C,
            InterfaceTiming = 0x40,
            MemoryAccessControl = 0x50,
            SlaveStatus = 0x60,
            SlaveDataCount = 0x64,
            Configuration = 0x7C 

        }
    }
}