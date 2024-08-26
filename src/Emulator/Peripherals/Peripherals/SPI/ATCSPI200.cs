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
                    .WithFlag(3,out leastSignificantByteFirst,name:"LSB")
                    .WithTaggedFlag("MOSIBiDir", 4)
                    .WithReservedBits(5, 2)
                    .WithFlag(7,out dataMerge,FieldMode.Read | FieldMode.Write, name: "DataMerge")
                    .WithValueField(8, 5,out dataLength,FieldMode.Read | FieldMode.Write, name: "DataLen")
                    .WithReservedBits(13, 3)
                    .WithEnumField<DoubleWordRegister, AddressLength>(16, 2,out addressLength,FieldMode.Read | FieldMode.Write, name: "AddrLen")
                    .WithReservedBits(18, 14)
                     
                }, 

                {(long)Registers.TransferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 8,out readCount, FieldMode.Read | FieldMode.Write, name: "RdTranCnt")
                    .WithValueField(9, 2,out dummyCount,FieldMode.Read | FieldMode.Write,name: "DummyCnt")
                    .WithFlag( 11,out tokenValue,name:"TokenValue")
                    .WithValueField(12, 9,out writeCount,FieldMode.Read | FieldMode.Write, name: "WrTranCnt")//,writeCallback: (_,__) =>{this.InfoLog("Write counts are {0}", writeCount.Value+1);})
                    .WithFlag(21,out tokenEn,name:"TokenEn")
                    .WithEnumField<DoubleWordRegister, DualQuad>(22, 2, out dualQuad, name: "DualQuad")
                    .WithEnumField<DoubleWordRegister, TransferMode>(24, 4, out transferMode, name: "TransMode")
                    .WithFlag(28,out addressformat, FieldMode.Read | FieldMode.Write,name:"AddrFmt")          
                    .WithFlag(29,out addressPhaseEnable, FieldMode.Read | FieldMode.Write, name: "AddrEn")
                    .WithFlag(30,out commandPhaseEnable, FieldMode.Read |FieldMode.Write, name: "CmdEn")
                    .WithTaggedFlag("SlvDataOnly", 31)
                    .WithWriteCallback((_, __) => {
                     var byteCount = (int)dataLength.Value / 8 + 1;
                      bytestoTransfer = ((int)(writeCount.Value+1))* (byteCount);
                       this.InfoLog(" in register bytes to Transfer are  {0}, write count{1},  byte count {2}", bytestoTransfer,writeCount.Value+1, byteCount);
                       this.InfoLog ("readcounts are {0}", readCount.Value+1);
                    })
                },

                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithValueField(0, 8,out command, FieldMode.Read |FieldMode.Write,name: "CMD",writeCallback: (_,__) => {transactionInProgress=true;})
                    .WithReservedBits(8, 24)
                    .WithChangeCallback((_, __) => {
                    TrySendData();})
                    
                },

                {(long)Registers.Address, new DoubleWordRegister(this)
                    .WithValueField(0, 32,out serialFlashAddress ,FieldMode.Read |FieldMode.Write, name: "Address")
                },
                
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32,FieldMode.Read |FieldMode.Write, name: "DATA",
                        writeCallback: (_, val) => {
                            EnqueueToTransmitBuffer((uint)val);      
                           this.InfoLog("transmit values is {0}",BitConverter.ToString(BitConverter.GetBytes(val))); 
                            this.InfoLog("transmit buffer count are {0}",txQueue.Count); 
                            
                           }
                       
                           ,
                       valueProviderCallback: _ =>
                    {   
                        if(!TryDequeueFromReceiveBuffer(out var data))
                        {
                            this.Log(LogLevel.Warning, "Trying to read from an empty FIFO");
                            return 0;
                        }

                        return data;
                    })

                    
                },     
                {(long)Registers.Control, new DoubleWordRegister(this)
                    .WithFlag(0, name: "SPIRST",
                        changeCallback: (_, value) => 
                        {  if(value)
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
                    .WithFlag(14,FieldMode.Read, name :"RXEMPTY")
                    .WithFlag(15,FieldMode.Read,name :"RXFULL",valueProviderCallback: (_) => rxQueue.Count == fifoSize )
                    .WithValueField(16, 6, name: "TXNUM")
                    .WithFlag(22,FieldMode.Read, name:"TXEMPTY",valueProviderCallback: (_) => txQueue.Count == 0)
                    .WithFlag(23,FieldMode.Read,name:"TXFULL",valueProviderCallback: (_) =>
                    
                    { if(txQueue.Count == fifoSize)
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

        private void EnqueueToTransmitBuffer(uint val)   //byte or ushort or uint
        {
            if(txQueue.Count == fifoSize)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full FIFO. Dropping the data");
                return;
            }

            txQueue.Enqueue(val);
            this.InfoLog("transmit values is {0}",BitConverter.ToString(BitConverter.GetBytes(val))); 

            if((uint)txQueue.Count == (uint)writeCount.Value+1 || (transactionInProgress && txQueue.Count == fifoSize) )
            {
                HandleByteTransmission();
            }
        }
        
        private void PerformTransaction(int size,bool readFromFifo,bool writeToFifo)
        {  
            var byteCount = (int)dataLength.Value / 8 + 1;
            var bytesfromslave = ((int)readCount.Value+1 ) *(byteCount);

            if(RegisteredPeripheral == null)
            {
                this.ErrorLog("Attempted to perform a UMA transaction with no peripheral attached");
                return;
            }
           
            if(commandPhaseEnable.Value)
            {
                RegisteredPeripheral.Transmit((byte)command.Value);
                this.InfoLog("Command is {0}  , and enable is {1}", command.Value, commandPhaseEnable.Value);

           }
           
           if(addressPhaseEnable.Value)
           {
                var a0 = (byte)(serialFlashAddress.Value);
                var a1 = (byte)(serialFlashAddress.Value>>8);
                var a2 = (byte)(serialFlashAddress.Value>>16);
                RegisteredPeripheral.Transmit(a2);
                RegisteredPeripheral.Transmit(a1);
                RegisteredPeripheral.Transmit(a0);

                this.InfoLog("addrress is {0} , {1} , {2}", a2, a1, a0);
           }
        if (readFromFifo){
          
          this.InfoLog("read from fifo");
        }

        else if(writeToFifo)
        {
            //for (var i=0; i<=bytesfromslave; i++)
           while((uint)rxQueue.Count!= (uint)readCount.Value+1)
           {
                HandleByteReception();
                
            }
        transactionInProgress = false;
        RegisteredPeripheral.FinishTransmission();
        }

        
         TryFinishTransmission();
        
                     
        }


         private void TrySendData(){
            switch(transferMode.Value){

                case TransferMode.WriteRead:
                    PerformTransaction(txQueue.Count, readFromFifo: true, writeToFifo: true);
                    this.InfoLog("Write Read");
                    break;
                case TransferMode.Write:
                    if (command.Value == 0x02)
                    {
                    PerformTransaction(txQueue.Count,readFromFifo: true, writeToFifo: false);
                    this.InfoLog("Write");
                    }
                     
                    if(command.Value==0xD8)
                    {
                        this.InfoLog("Erase");
                    }

                    break;
                case TransferMode.Read:
                    PerformTransaction(rxQueue.Count,readFromFifo: false, writeToFifo: true);
                    this.InfoLog("Read");
                    break;
                default:
                    PerformTransaction(txQueue.Count,readFromFifo: true, writeToFifo: true);
                    this.InfoLog("Send data by default");
                    break;
            }

        }

        private void HandleByteTransmission(){

            var byteCount = (int)dataLength.Value / 8 + 1;
            var bytes = new byte[MaxPacketBytes];
            var reverseBytes = BitConverter.IsLittleEndian; 

          bytestoTransfer = bytestoTransfer - (int)txQueue.Count;
            this.InfoLog("bytes to Transfer are  {0}, queue count{1}, write count{2}", bytestoTransfer,txQueue.Count,writeCount.Value+1);
            while(txQueue.Count!=0)
          {
            var value = txQueue.Dequeue();
            BitHelper.GetBytesFromValue(bytes, 0, value, byteCount, reverseBytes);
            for(var i = 0; i < byteCount; i++)
                {   
                    
                    bytes[i] = RegisteredPeripheral.Transmit(bytes[i]);
                    this.InfoLog("values in transmit loop {0}", bytes[i]);
                }
            rxQueue.Enqueue(BitHelper.ToUInt32(bytes, 0, byteCount, reverseBytes));
          }
          txQueue.Clear();

          if(bytestoTransfer!=0){
            return;
          }
          transactionInProgress=false;
          RegisteredPeripheral.FinishTransmission();


        }
        
        private void HandleByteReception()
        {
            var receivedByte = RegisteredPeripheral.Transmit(0);
           
            rxQueue.Enqueue(receivedByte);
        }

         private void TryFinishTransmission()
        {   
            if ( transferMode.Value == TransferMode.Write || transferMode.Value ==TransferMode.Read)
            {
                 this.InfoLog("return from TryFinishtransmission");
                 return;
            }
            transactionInProgress = false;
            RegisteredPeripheral.FinishTransmission();
       
            
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
        private IFlagRegisterField leastSignificantByteFirst;
        private IFlagRegisterField dataMerge;
        private IFlagRegisterField tokenEn;
        private IFlagRegisterField tokenValue;
        


        private IEnumRegisterField<AddressLength> addressLength;
        private IEnumRegisterField<TransferMode> transferMode;
         private IEnumRegisterField<DualQuad> dualQuad;


        private readonly uint fifoSize;   
        private uint sizeLeft;  
        private const int FIFODataWidth = 0x04;
        private const int FIFOLength = 32;
        private const int MaximumNumberOfSlaves = 4;
        private const int MaxPacketBytes = 4;
        private bool transactionInProgress;
        private bool txFull = false;
        private int bytestoTransfer;


        private readonly Queue<uint> rxQueue;  
        private readonly Queue<uint> txQueue;  
        private readonly DoubleWordRegisterCollection registers;
        private readonly object innerLock = new object();

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

                       
 
/*(Virgo) spi.Flash1 Transmit 0x06
0x00
(Virgo) spi.Flash1 FinishTransmission
(Virgo) sysbus.spi WriteDoubleWord 0x28 0x000000
(Virgo) sysbus.spi WriteDoubleWord 0x20 0x60000000
(Virgo) sysbus.spi WriteDoubleWord 0x2C 0x12
(Virgo) sysbus.spi WriteDoubleWord 0x2C 0x12
(Virgo) sysbus.spi WriteDoubleWord 0x2C 0x12
(Virgo) sysbus.spi WriteDoubleWord 0x24 0x02*/