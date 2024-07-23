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
                    .WithTaggedFlag("LSB", 3)
                    .WithTaggedFlag("MOSIBiDir", 4)
                    .WithReservedBits(5, 2)
                    .WithFlag(7, name: "DataMerge",
                        writeCallback: (_, value) => {this.InfoLog("Data Merge feild");})
                    .WithValueField(8, 5,out dataLength, name: "DataLen")
                       //, writeCallback: (_, value) => {this.InfoLog("Data Length");})
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 2,out addressLength, name: "AddrLen")
                       //, writeCallback: (_, value) => {this.InfoLog("Address Length");})
                    .WithReservedBits(18, 14)
                     
                }, 

                {(long)Registers.TransferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 8,FieldMode.Read, name: "RdTranCnt",
                        valueProviderCallback: _ => (uint)rxQueue.Count)
                    .WithValueField(9, 2, name: "DummyCnt",
                        writeCallback: (_, value) => {this.InfoLog("Dummy data count");})
                    .WithTaggedFlag("TokenValue", 11)
                    .WithValueField(12, 9, name: "WrTranCnt",
                        writeCallback: (_, value) => {this.InfoLog("Transfer count for write data");})
                    .WithTaggedFlag("TokenEn", 21)
                    .WithTag("DualQuad", 22, 2)
                    .WithTag("TransMode", 24, 4)  //TODO add enum feild
                    .WithTaggedFlag("AddrFmt",28)          
                    .WithFlag(29,out addressPhaseEnable, name: "AddrEn")
                    .WithFlag(30,out commandPhaseEnable , name: "CmdEn")
                    .WithTaggedFlag("SlvDataOnly", 31)
                },

                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithValueField(0, 8,out command, name: "CMD",
                        writeCallback: (_, value) => {
                        PerformTransaction((int)addressLength.Value, (int)dataLength.Value);
                            this.InfoLog("Command enable");})
                    .WithReservedBits(8, 24)
                },

                {(long)Registers.Address, new DoubleWordRegister(this)
                    .WithValueField(0, 32,out serialFlashAddress , name: "Address")
                },
                
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 32, name: "DATA",
                        writeCallback: (_, val) => {
                            EnqueueToTransmitBuffer((uint)val);  //byte/ushort/uint
                           },
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
                        writeCallback: (_, value) => {this.InfoLog("SPI reset");})
                    .WithFlag(1, name: "RXFIFORST",
                        writeCallback: (_, value) => {this.InfoLog("Receive FIFO reset");})
                    .WithFlag(2, name: "TXFIFORST",
                        writeCallback: (_, value) => {this.InfoLog("Transmit FIFO reset");})
                    .WithTaggedFlag("RXDMAEN",3)
                    .WithTaggedFlag("TXDMAEN",4)
                    .WithReservedBits(5, 3)
                    .WithValueField(8, 8, out rxFIFOThreshold, name: "RXTHRES")
                    .WithValueField(16, 5, out txFIFOThreshold, name: "TXTHRES")
                    .WithReservedBits(24, 8)     
                },
      

                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    //TODO: use enum/case
                    .WithValueField(0, 4, FieldMode.Read, name: "RXFIFO - Receive FIFO Size (in words; size = 2^RXFIFO)",
                    valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
                    )
                    .WithValueField(4, 4, FieldMode.Read, name: "TXFIFO - Transmit FIFO Size (in words; size = 2^TXFIFO)",
                    valueProviderCallback: _ => (ulong)Misc.Logarithm2((int)fifoSize)
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
       
        /*private uint GetDataLength()
        {
            // frameSize keeps value substracted by 1
            var sizeLeft = (uint)dataLength.Value + 1;
            if(sizeLeft % 8 != 0)
            {
                sizeLeft += 8 - (sizeLeft % 8);
                this.Log(LogLevel.Warning, "Only 8-bit-aligned transfers are currently supported, but data length is set to {0}, adjusting it to: {1}", dataLength.Value, sizeLeft);
            }

            return sizeLeft;
        }*/
        
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
        }
        
      
        private void PerformTransaction(int addressWidth, int dataWidth )
        {
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
           
         
           // if(command.Value == 0x03)
           // {
           // lock(innerLock)
           // {
            for (var i = 0; i < 8; i++)
            {
              HandleByteReception();
            }
            this.InfoLog(" command value is {0}", command.Value);
            TryFinishTransmission();
           // }
           // }
                     
        }
        private void HandleByteReception()
        {
            var receivedByte = RegisteredPeripheral.Transmit(0);
           
            rxQueue.Enqueue(receivedByte);
        }

         private void TryFinishTransmission()
        {
            //TODO: put conditions
            RegisteredPeripheral.FinishTransmission();
            
        }
        
        [ConnectionRegionAttribute("xip")]
        public uint XipReadDoubleWord(long offset)
        {

            return (RegisteredPeripheral as IDoubleWordPeripheral)?.ReadDoubleWord(offset) ?? 0;
        }
        [ConnectionRegionAttribute("xip")]
        public void XipWriteDoubleWord(long offset, uint value)
        {
            this.Log(LogLevel.Warning, "Trying to write 0x{0:X} to XIP region at offset 0x{1:x}. Direct writing is not supported", value, offset);
        }

       
        private IValueRegisterField dataLength;  
        private IValueRegisterField command; 
        private IValueRegisterField serialFlashAddress; 
        private IValueRegisterField addressLength;
        private IValueRegisterField rxFIFOThreshold;
        private IValueRegisterField txFIFOThreshold;
        
        private IFlagRegisterField commandPhaseEnable; 
        private IFlagRegisterField addressPhaseEnable;

        private readonly uint fifoSize;   
        private uint sizeLeft;  
        private const int FIFODataWidth = 0x04;
        private const int FIFOLength = 32;
        private const int MaximumNumberOfSlaves = 4;
   
        private readonly Queue<uint> rxQueue;  
        private readonly Queue<uint> txQueue;  
        private readonly DoubleWordRegisterCollection registers;

        private const byte DummyResponseByte = 0xFF;
        private readonly object innerLock = new object();

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
            Configuration = 0x7C  //done

        }
    }
}

//IMXRT_LPSPI.cs         TXFIFORST ,RXFIFORST -Reset fields (SPI Control Register)
                        //fifosize (LPSPI)
                        //fifodepth(ATCSPI)

                        //framesize(LPSPI)
                        //datalength(atcspi)
                         //wordcount

//DesignWareSPI        //Transfermode(Designware) transmit/receive
                       //Transfermode(ATCSPI) readwrite
                       
                      //transfer size /framesize,no of frames (Designware)
                      //datalength(ATC)

                      //Data register
                      //receivebuffer count , transmitbuffer count (Designware)
                      //read count,write count  , dummy missing (ATCSPI
                       


