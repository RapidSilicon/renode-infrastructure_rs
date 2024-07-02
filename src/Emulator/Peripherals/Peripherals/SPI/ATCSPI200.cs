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
    public class ATCSPI200 : NullRegistrationPointPeripheralContainer<Micron_MT25Q>, IDoubleWordPeripheral, IKnownSize /*IWordPeripheral, IBytePeripheral ,NullRegistrationPointPeripheralContainer<ISPIFlash>*/
    {
        public ATCSPI200(IMachine machine, uint fifoSize, bool hushTxFifoLevelWarnings = false) : base(machine)
        {
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            rxQueue = new Queue<uint>();
            txQueue = new Queue<uint>();
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

       /* private void UpdateInterrupts()
        {
            interruptRxLevelPending.Value = rxQueue.Count >= (int)rxFIFOThreshold.Value;
            interruptTxLevelPending.Value = txQueue.Count >= (int)txFIFOThreshold.Value;

            var pending = false;
            pending |= interruptTxLevelEnabled.Value && interruptTxLevelPending.Value;
            pending |= interruptTxEmptyEnabled.Value && interruptTxEmptyPending.Value;
            pending |= interruptRxLevelEnabled.Value && interruptRxLevelPending.Value;
            pending |= interruptRxFullEnabled.Value && interruptRxFullPending.Value;
            pending |= interruptTransactionFinishedEnabled.Value && interruptTransactionFinishedPending.Value;
            pending |= interruptRxOverrunEnabled.Value && interruptRxOverrunPending.Value;
            pending |= interruptRxUnderrunEnabled.Value && interruptRxUnderrunPending.Value;
            IRQ.Set(pending);
        }

        private void StartTransaction()
        {
            // deassert CS of active peripherals that are not enabled in the slave select register anymore
            DeassertCS(x => !BitHelper.IsBitSet((uint)slaveSelect.Value, (byte)x));

            foreach(var value in txQueue)
            {
                Transmit(value);
            }

            txQueue.Clear();

            transactionInProgress = true;
            interruptTxEmptyPending.Value = true;

            UpdateInterrupts();
            TryFinishTransaction();
        }

        private void TryFinishTransaction()
        {
            if(charactersToTransmit > 0)
            {
                return;
            }

            transactionInProgress = false;
            interruptTransactionFinishedPending.Value = true;

            // deassert CS of active peripherals marked in the should deassert array
            DeassertCS(x => shouldDeassert[x]);
            UpdateInterrupts();
        }

        private void Transmit(byte value)
        {
            var numberOfPeripherals = ActivePeripherals.Count();
            foreach(var indexPeripheral in ActivePeripherals)
            {
                var peripheral = indexPeripheral.Item2;
                var output = peripheral.Transmit(value);
                // In case multiple SS lines are chosen, we are deliberately
                // ignoring output from all of them. Therefore, this configuration
                // can only be used to send data to multiple receivers at once.
                if(numberOfPeripherals == 1)
                {
                    RxEnqueue(output);
                }
            }

            if(numberOfPeripherals == 0)
            {
                // If there is no target device we still need to populate the RX queue
                // with dummy bytes
                RxEnqueue(DummyResponseByte);
            }

            charactersToTransmit -= 1;
            TryFinishTransaction();
        }

        private void TryTransmit()
        {
            if(!transactionInProgress || rxQueue.Count == FIFOLength || txQueue.Count == 0)
            {
                return;
            }

            var bytesToTransmit = Math.Min(FIFOLength - rxQueue.Count, txQueue.Count);
            for(var i = 0; i < bytesToTransmit; ++i)
            {
                Transmit(txQueue.Dequeue());
            }
        }

        private void RxEnqueue(byte value)
        {
            if(!rxFIFOEnabled.Value)
            {
                return;
            }

            if(rxQueue.Count == FIFOLength)
            {
                interruptRxOverrunPending.Value = true;
                UpdateInterrupts();
                return;
            }
            rxQueue.Enqueue(value);
            if(rxQueue.Count == FIFOLength)
            {
                interruptRxFullPending.Value = true;
                UpdateInterrupts();
            }
        }

        private byte RxDequeue()
        {
            if(!rxFIFOEnabled.Value)
            {
                this.Log(LogLevel.Warning, "Tried to read from RX FIFO while it's disabled");
                return 0x00;
            }

            if(!rxQueue.TryDequeue(out var result))
            {
                interruptRxUnderrunPending.Value |= true;
            }
            else
            {
                TryTransmit();
            }

            TryFinishTransaction();
            UpdateInterrupts();

            return result;
        }

        private void TxEnqueue(byte value)
        {
            if(transactionInProgress && rxQueue.Count < FIFOLength)
            {
                // If we have active transaction and we have room to receive data,
                // send/receive it immediately
                Transmit(value);
            }
            else
            {
                // Otherwise, we either generate TX overrun interrupt if internal
                // TX buffer is full, or enqueue new data to it. This data will be
                // send either after START condition, or when there is room in RX
                // buffer when transaction is active
                if(txQueue.Count == FIFOLength)
                {
                    interruptTxOverrunPending.Value = true;
                }
                else
                {
                    txQueue.Enqueue(value);
                }
            }
            UpdateInterrupts();
        }*/

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
                    .WithValueField(8, 5,out dataLength, name: "DataLen",
                        writeCallback: (_, value) => {this.InfoLog("Data Length");})
                    .WithReservedBits(13, 3)
                    .WithValueField(16, 2, name: "AddrLen",
                        writeCallback: (_, value) => {this.InfoLog("Address Length");})
                    .WithReservedBits(18, 14)
                     
                }, 
                {(long)Registers.TransferControl, new DoubleWordRegister(this)
                    .WithValueField(0, 8, name: "RdTranCnt",
                        writeCallback: (_, value) => {this.InfoLog("Transfer count for read data");})
                    .WithValueField(9, 2, name: "DummyCnt",
                        writeCallback: (_, value) => {this.InfoLog("Dummy data count");})
                    .WithTaggedFlag("TokenValue", 11)
                    .WithValueField(12, 9, name: "WrTranCnt",
                        writeCallback: (_, value) => {this.InfoLog("Transfer count for write data");})
                    .WithTaggedFlag("TokenEn", 21)
                    .WithTag("DualQuad", 22, 2)
                    .WithTag("TransMode", 24, 4)  //TODO add enum feild
                    .WithFlag(28, name: "AddrFmt",
                        writeCallback: (_, value) => {this.InfoLog("Address phase format");})
                    .WithFlag(29, name: "AddrEn",
                        writeCallback: (_, value) => {this.InfoLog("Address phase enable");})
                    .WithFlag(30, name: "CmdEn",
                        writeCallback: (_, value) => {this.InfoLog("Command phase enable");})
                    .WithTaggedFlag("SlvDataOnly", 31)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithValueField(0, 7, name: "CMD",
                        writeCallback: (_, value) => {this.InfoLog("Command enable");})
                    .WithReservedBits(8, 24)
                },
                 {(long)Registers.Address, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: "Address",
                        writeCallback: (_, value) => {this.InfoLog("Address Register");})
                },
                //SPI Data Register ATC
                //SPI FIFO Data Registers MAX
                //TODO:rework required
                {(long)Registers.Data, new DoubleWordRegister(this)
                    .WithValueField(0, 31, name: "DATA",
                        writeCallback: (_, val) => {
                            EnqueueToTransmitBuffer((uint)val);  //byte/ushort/uint
                            this.InfoLog("Data Register");},
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
      
                /*{(long)Registers.InterruptStatusFlags, new DoubleWordRegister(this)
                    //TX FIFO Threshold Level Crossed Flag(MAX)
                    //TX FIFO Threshold interruptO(ATC)
                    .WithFlag(0, out interruptTxLevelPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_level")
                    //TX FIFO Empty Flag(MAX)
                    //Transmit FIFO Empty flag(ATC)SPI Status Register 
                    .WithFlag(1, out interruptTxEmptyPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_empty")
                    //RX FIFO Threshold Level Crossed Flag(MAX)
                    //RX FIFO Threshold interrupt(ATC)
                    .WithFlag(2, out interruptRxLevelPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_level")
                    //RX FIFO Full Flag(MAX)
                    //Receive FIFO Full flag(ATC)
                    .WithFlag(3, out interruptRxFullPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_full")
                    .WithTaggedFlag("INT_FL.ssa", 4)
                    .WithTaggedFlag("INT_FL.ssd", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("INT_FL.fault", 8)
                    .WithTaggedFlag("INT_FL.abort", 9)
                    .WithReservedBits(10, 1)
                    .WithFlag(11, out interruptTransactionFinishedPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.m_done")
                    .WithFlag(12, out interruptTxOverrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.tx_ovr")
                    .WithTaggedFlag("INT_FL.tx_und", 13)
                    .WithFlag(14, out interruptRxOverrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_FL.rx_ovr")
                    .WithFlag(15, out interruptRxUnderrunPending, FieldMode.Read | FieldMode.WriteOneToClear, name: "INT_EN.rx_und")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                //SPI Interrupt Enable Register(MAX)
                //SPI Interrupt Enable Register(ATC)
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    //TX FIFO Threshold Level Crossed Interrupt Enable(MAX)
                    //Enable the SPI Transmit FIFO Threshold interrupt(ATC)
                    .WithFlag(0, out interruptTxLevelEnabled, name: "INT_EN.tx_level")
                    .WithFlag(1, out interruptTxEmptyEnabled, name: "INT_EN.tx_empty")
                    //RX FIFO Threshold Level Crossed Interrupt Enable(MAX)
                    //SPI Receive FIFO Threshold interrupt(ATC)
                    .WithFlag(2, out interruptRxLevelEnabled, name: "INT_EN.rx_level")
                    .WithFlag(3, out interruptRxFullEnabled, name: "INT_EN.rx_full")
                    //slave commands
                    .WithTaggedFlag("INT_EN.ssa", 4)
                    .WithTaggedFlag("INT_EN.ssd", 5)
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("INT_EN.fault", 8)
                    .WithTaggedFlag("INT_EN.abort", 9)
                    .WithReservedBits(10, 1)
                    //Master Data Transmission Done Interrupt Enable(MAX)
                    //Enable the End of SPI Transfer interrupt
                    .WithFlag(11, out interruptTransactionFinishedEnabled, name: "INT_EN.m_done")
                    .WithFlag(12, out interruptTxOverrunEnabled, name: "INT_EN.tx_ovr")
                    //TX FIFO Underrun Interrupt Enable (MAX)
                    //SPI Transmit FIFO Underrun interrupt(ATC Slave only)
                    .WithTaggedFlag("INT_EN.tx_und", 13)
                    //RX FIFO Overrun Interrupt Enable(MAX)
                    // SPI Receive FIFO Overrun interrupt(ATC Slave only)
                    .WithFlag(14, out interruptRxOverrunEnabled, name: "INT_EN.rx_ovr")
                    //RX FIFO Underrun Interrupt Enable(MAX)
                    .WithFlag(15, out interruptRxUnderrunEnabled, name: "INT_EN.rx_und")
                    .WithReservedBits(16, 16)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                //SPI Status Registers(ATCSPI)
                //SPI Status Registers(MAX)
                {(long)Registers.ActiveStatus, new DoubleWordRegister(this)
                    .WithTaggedFlag("STAT.busy", 0)
                    .WithReservedBits(1, 31)
                } */

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

           /* {
                var constructedRegister = new DoubleWordRegister(this)
                     //SPI Control Register ATC(TXTHRES,RXTHRES)
                    // SPI DMA Control Registers MAX(tx_fifo_level,rx_fifo_level)
                    .WithValueField(0, 5, out txFIFOThreshold, name: "DMA.tx_fifo_level")
                    // NOTE: 5th bit covered in if statement
                    .WithFlag(6, out txFIFOEnabled, name: "DMA.tx_fifo_en")
                    .WithFlag(7, FieldMode.WriteOneToClear, name: "DMA.tx_fifo_clear",
                        writeCallback: (_, value) => { if(value) txQueue.Clear(); })
                    //Number of Bytes in the TX FIFO(MAX)
                    //Number of valid entries in the Transmit FIFO(ATC)
                    .WithValueField(8, 6, FieldMode.Read, name: "DMA.tx_fifo_cnt",
                        valueProviderCallback: _ => (uint)txQueue.Count)
                    .WithReservedBits(14, 1)
                     //SPI Control Register ATC
                    // SPI DMA Control Registers MAX
                    .WithTaggedFlag("DMA.tx_dma_en", 15)
                    .WithValueField(16, 5, out rxFIFOThreshold, name: "DMA.rx_fifo_level")
                    .WithReservedBits(21, 1)
                    .WithFlag(22, out rxFIFOEnabled, name: "DMA.rx_fifo_en")
                    .WithFlag(23, FieldMode.WriteOneToClear, name: "DMA.rx_fifo_clear",
                        writeCallback: (_, value) => { if(value) rxQueue.Clear(); })
                        //Number of Bytes in the RX FIFO(MAX)
                        //Number of valid entries in the Receive FIFO(ATC)
                    .WithValueField(24, 6, FieldMode.Read, name: "DMA.rx_fifo_cnt",
                        valueProviderCallback: _ => (uint)rxQueue.Count)
                    .WithReservedBits(30, 1)
                    .WithTag("DMA.rx_dma_en", 31, 1)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                ;
                // Depending on the peripheral constructor argument, treat writes to reserved field as error or don't.
                if(hushTxFifoLevelWarnings)
                {
                    constructedRegister.WithFlag(5, name: "RESERVED");
                }
                else
                {
                    constructedRegister.WithReservedBits(5, 1);
                }
                registerMap.Add((long)Registers.DMAControl, constructedRegister);
            }*/

            return registerMap;
        }
       
        private uint GetDataLength()
        {
            // frameSize keeps value substracted by 1
            var sizeLeft = (uint)dataLength.Value + 1;
            if(sizeLeft % 8 != 0)
            {
                sizeLeft += 8 - (sizeLeft % 8);
                this.Log(LogLevel.Warning, "Only 8-bit-aligned transfers are currently supported, but data length is set to {0}, adjusting it to: {1}", dataLength.Value, sizeLeft);
            }

            return sizeLeft;
        }
        
         public bool TryDequeueFromReceiveBuffer(out uint data)
        {
            if(!rxQueue.TryDequeue(out data))
            {
               // receiveUnderflow.Value = true;
               // UpdateInterrupt();

                data = 0;
                return false;
            }

        

          /*  if(receiveBuffer.Count <= receiveThreshold)
            {
                receiveFull.Value = false;
                UpdateInterrupt();
            }*/

            return true;
        }

        private void EnqueueToTransmitBuffer(uint val)   //byte or ushort or uint
        {
            if(txQueue.Count == fifoSize)
            {
                this.Log(LogLevel.Warning, "Trying to write to a full FIFO. Dropping the data");
                //transmitOverflow.Value = true;  TODO : invoke interrupt
                //UpdateInterrupt();
                return;
            }

            txQueue.Enqueue(val);

            /*if(transmitBuffer.Count <= transmitThreshold)
            {
                transmitEmpty.Value = true;
                UpdateInterrupt();
            }*/
        }
        
        private void sendCommand(uint command) //uint length
        {  
            if(sizeLeft == 0)
            {
                // let's assume this is a new transfer
                this.Log(LogLevel.Debug, "Starting a new SPI xfer, frame size: {0} bytes", GetDataLength() / 8);
                sizeLeft = GetDataLength();
                if(command==0x02)  //write command
                {
                    while (sizeLeft != 0 )
                    { 
                        foreach(var value in txQueue)
                        {
                            RegisteredPeripheral.Transmit((byte)value);
                        }
                        txQueue.Clear();
                    }
                    TryFinishTransmission();
                }
                 
                if(command==0x03)   //read command
                {
                   
                    while (sizeLeft != 0 )
                    { 
                        HandleByteReception();
                    }
                }
                
           
            }

        }  
        private void HandleByteReception()
        {
            var receivedByte = RegisteredPeripheral.Transmit(0);
            rxQueue.Enqueue(receivedByte);
            TryFinishTransmission();
        }

         private void TryFinishTransmission()
        {
            //TODO: put conditions
            RegisteredPeripheral.FinishTransmission();
            
        }
         

        private bool[] shouldDeassert;
        private bool transactionInProgress;
        private bool hushTxFifoLevelWarnings;
        private uint charactersToTransmit;

        private IValueRegisterField slaveSelect;

        private IFlagRegisterField rxFIFOEnabled;
        private IFlagRegisterField txFIFOEnabled;

        private IValueRegisterField rxFIFOThreshold;
        private IValueRegisterField txFIFOThreshold;
        private IValueRegisterField dataLength;  //updated

        private IFlagRegisterField interruptTxLevelPending;
        private IFlagRegisterField interruptTxEmptyPending;
        private IFlagRegisterField interruptRxLevelPending;
        private IFlagRegisterField interruptRxFullPending;
        private IFlagRegisterField interruptTransactionFinishedPending;
        private IFlagRegisterField interruptTxOverrunPending;
        private IFlagRegisterField interruptRxOverrunPending;
        private IFlagRegisterField interruptRxUnderrunPending;

        private IFlagRegisterField interruptTxLevelEnabled;
        private IFlagRegisterField interruptTxEmptyEnabled;
        private IFlagRegisterField interruptRxLevelEnabled;
        private IFlagRegisterField interruptRxFullEnabled;
        private IFlagRegisterField interruptTransactionFinishedEnabled;
        private IFlagRegisterField interruptTxOverrunEnabled;
        private IFlagRegisterField interruptRxOverrunEnabled;
        private IFlagRegisterField interruptRxUnderrunEnabled;
        
        private readonly uint fifoSize;   //updated
        private uint sizeLeft;  //updated
        private const int FIFODataWidth = 0x04;
        private const int FIFOLength = 32;
        private const int MaximumNumberOfSlaves = 4;

        private readonly Queue<uint> rxQueue;  //updated
        private readonly Queue<uint> txQueue;  //updated
        private readonly DoubleWordRegisterCollection registers;

        private const byte DummyResponseByte = 0xFF;

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
                       
//MPFS                     slave commands

//Questions
//memory mapped interface?
//genericspiflash  Addressing/dummy bytes idea?
//datalength maximum =32
//SPI command register(encoding commands):come from flash
