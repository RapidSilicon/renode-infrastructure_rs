//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class ATCDMAC100 : BasicDoubleWordPeripheral, IKnownSize
    {
        public ATCDMAC100(IMachine machine) : base(machine)
        {
            engine = new DmaEngine(sysbus);
            IRQ = new GPIO();
            channels = new Channel[NumberOfChannels];
            for (var i = 0; i < NumberOfChannels; ++i)
            {
                channels[i] = new Channel(this, i);
            }
            BuildRegisters();
        }

        public override void Reset()
        {
            foreach (var channel in channels)
            {
                channel.Reset();
            }
            base.Reset();
            UpdateInterrupts();
        }

        public GPIO IRQ { get; }

        public long Size => 0x400;
        
        private void BuildRegisters()
        {
            Registers.Configuration.Define(this)
                .WithValueField(0, 4, FieldMode.Read, name: "CHANNELNUM",
                 valueProviderCallback: _ => NumberOfChannels)
                .WithTag("FIFODEPTH", 4, 5)
                .WithTag("REQUESTNUM", 10, 5)
                .WithReservedBits(16, 14)
                .WithTaggedFlag("REQUESTSYNC", 30)
                .WithTaggedFlag("CHAINXFR", 31)
            ;
            Registers.Control.Define(this)
                .WithFlag(0, FieldMode.Read | FieldMode.Write, writeCallback: (_, value) => { if (value) Reset(); }, name: "RESET")
                .WithReservedBits(1, 31)
            ;
            Registers.InterruptStatus.Define(this)
                .WithFlags(0, 8, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: (i, _, value) => channels[i].errorstatus &= !value, valueProviderCallback: (i, _) => channels[i].errorstatus, name: "ERROR")
                .WithFlags(8, 8, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: (i, _, value) => channels[i].abortstatus &= !value, valueProviderCallback: (i, _) => channels[i].abortstatus, name: "ABORT")
                .WithFlags(16, 8, FieldMode.WriteOneToClear | FieldMode.Read, writeCallback: (i, _, value) => channels[i].interruptTCstatus &= !value, valueProviderCallback: (i, _) => channels[i].interruptTCstatus, name: "TC")
                .WithReservedBits(24, 8)
                .WithWriteCallback((_, __) => UpdateInterrupts())
            ;
            Registers.ChannelEnable.Define(this)
                .WithFlags(0, 8, valueProviderCallback: (i, _) => channels[i].ChEN, name: "CHEN")
                .WithReservedBits(8, 24)
            ;
            Registers.ChannelAbort.Define(this)
                .WithFlags(0, 8, FieldMode.Read | FieldMode.Write, writeCallback: (i, _, value) => channels[i].ChAbort = value, valueProviderCallback: (i, _) => channels[i].ChAbort, name: "CHABORT")
                .WithReservedBits(8, 24)
            ;
            var channelDelta = (uint)((long)Registers.Channel1Control - (long)Registers.Channel0Control);
            Registers.Channel0Control.BindMany(this, NumberOfChannels, i => channels[i].ControlRegister, channelDelta);
            Registers.Channel0SourceAddress.BindMany(this, NumberOfChannels, i => channels[i].SourceAddressRegister, channelDelta);
            Registers.Channel0DestinationAddress.BindMany(this, NumberOfChannels, i => channels[i].DestinationAddressRegister, channelDelta);
            Registers.Channel0TransferSize.BindMany(this, NumberOfChannels, i => channels[i].TransferSizeRegister, channelDelta);
            Registers.Channel0LinkListPointer.BindMany(this, NumberOfChannels, i => channels[i].LinkListPointerRegister, channelDelta);
        }

        private void UpdateInterrupts()
        {
            IRQ.Set(channels.Any(channel => channel.IRQ));
            if (IRQ.IsSet)
            {
                this.Log(LogLevel.Info, "Interrupt set for channels: {0}", String.Join(", ",
                    channels
                        .Where(channel => channel.IRQ)
                        .Select(channel => channel.Index)
                    ));
            }
            else
            {
                this.Log(LogLevel.Info, "Interrupt unset for channel");
            }
        }

        private readonly DmaEngine engine;
        private readonly Channel[] channels;
        private const int NumberOfChannels = 8;
        private enum Registers
        {
            Configuration = 0x10,
            Control = 0x20,
            InterruptStatus = 0x30,
            ChannelEnable = 0x34,
            ChannelAbort = 0x40,
            Channel0Control = 0x44,
            Channel0SourceAddress = 0x48,
            Channel0DestinationAddress = 0x4C,
            Channel0TransferSize = 0x50,
            Channel0LinkListPointer = 0x54,
            Channel1Control = 0x44 + 0x14,   //0x58
            Channel1SourceAddress = 0x48 + 0x14, //0x5C
            Channel1DestinationAddress = 0x4C + 0x14, //0X60
            Channel1TransferSize = 0x50 + 0x14,  //0x64
            Channel1LinkListPointer = 0x54 + 0x14, //0x68
            Channel2Control = 0x44 + 2 * 0x14, //0x6C
            Channel2SourceAddress = 0x48 + 2 * 0x14,//0x70
            Channel2DestinationAddress = 0x4C + 2 * 0x14,//0x74
            Channel2TransferSize = 0x50 + 2 * 0x14, //0x78
            Channel2LinkListPointer = 0x54 + 2 * 0x14, //0x7C
            Channel3Control = 0x44 + 3 * 0x14,
            Channel3SourceAddress = 0x48 + 3 * 0x14,
            Channel3DestinationAddress = 0x4C + 3 * 0x14,
            Channel3TransferSize = 0x50 + 3 * 0x14,
            Channel3LinkListPointer = 0x54 + 3 * 0x14,
            Channel4Control = 0x44 + 4 * 0x14,
            Channel4SourceAddress = 0x48 + 4 * 0x14,
            Channel4DestinationAddress = 0x4C + 4 * 0x14,
            Channel4TransferSize = 0x50 + 4 * 0x14,
            Channel4LinkListPointer = 0x54 + 4 * 0x14,
            Channel5Control = 0x44 + 5 * 0x14,
            Channel5SourceAddress = 0x48 + 5 * 0x14,
            Channel5DestinationAddress = 0x4C + 5 * 0x14,
            Channel5TransferSize = 0x50 + 5 * 0x14,
            Channel5LinkListPointer = 0x54 + 5 * 0x14,
            Channel6Control = 0x44 + 6 * 0x14,
            Channel6SourceAddress = 0x48 + 6 * 0x14,
            Channel6DestinationAddress = 0x4C + 6 * 0x14,
            Channel6TransferSize = 0x54 + 6 * 0x14,
            Channel6LinkListPointer = 0x58 + 6 * 0x14,
            Channel7Control = 0x44 + 7 * 0x14,
            Channel7SourceAddress = 0x48 + 7 * 0x14,
            Channel7DestinationAddress = 0x4C + 7 * 0x14,
            Channel7TransferSize = 0x50 + 7 * 0x14,
            Channel7LinkListPointer = 0x54 + 7 * 0x14,
        }

        private class Channel
        {
            public Channel(ATCDMAC100 parent, int index)
            {
                this.parent = parent;
                Index = index;
                descriptor = default(Descriptor);

                ControlRegister = new DoubleWordRegister(parent)
                    .WithFlag(0, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>
                       {
                           descriptor.Enabled = value;
                           if (descriptor.Enabled && descriptor.TranSize == 0)
                           {
                               parent.ErrorLog("Attempted to perform a DMA transaction with invalid transfer size");
                               errorstatus = true;
                           }
                       },
                       name: "ENABLE")
                    .WithFlag(1, out InterruptTCMask, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) =>  descriptor.interruptTCMask = value,
                        valueProviderCallback: _ => descriptor.interruptTCMask,
                        name: "INTTCMASK")
                    .WithFlag(2, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) => descriptor.InterruptErrMask = value,
                        name: "INTERRMASK")
                    .WithFlag(3, FieldMode.Read | FieldMode.Write,
                       writeCallback: (_, value) => descriptor.InterruptAbtMask = value,
                       name: "INTABTMASK")
                    .WithValueField(4, 4,
                        writeCallback: (_, value) => descriptor.DstReqSel = (uint)value,
                        name: "DSTREQSEL")
                    .WithValueField(8, 4,
                       writeCallback: (_, value) => descriptor.SrcReqSel = (uint)value,
                       name: "SRCREQSEL")
                    .WithEnumField<DoubleWordRegister, AddressMode>(12, 2, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            descriptor.DstAddrCtrl = value;
                            parent.InfoLog("Destination address : {0}", descriptor.DstAddrCtrl);
                        },
                        name: "DSTADDCTRL")
                    .WithEnumField<DoubleWordRegister, AddressMode>(14, 2, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            descriptor.SrcAddrCtrl = value;
                            parent.InfoLog("Source address : {0}", descriptor.SrcAddrCtrl);
                        },
                        name: "SRCADDCTRL")
                    .WithFlag(16, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) => descriptor.DstMode = value,
                        name: "DSTMODE")
                    .WithFlag(17, FieldMode.Write | FieldMode.Read,
                        writeCallback: (_, value) => descriptor.SrcMode = value,
                        name: "SRCMODE")
                    .WithEnumField<DoubleWordRegister, SizeMode>(18, 2,
                        writeCallback: (_, value) =>
                        {
                            descriptor.dstwidth = value;
                            parent.InfoLog("Destination width : {0}", descriptor.dstwidth);
                        },
                        valueProviderCallback: _ => descriptor.dstwidth,
                        name: "DSTWIDTH")
                    .WithEnumField<DoubleWordRegister, SizeMode>(20, 2,
                        writeCallback: (_, value) =>
                        {
                            descriptor.srcwidth = value;
                            parent.InfoLog("Source width : {0} ", descriptor.srcwidth);
                        },
                        valueProviderCallback: _ => descriptor.srcwidth,
                        name: "SRCWIDTH")
                    .WithEnumField<DoubleWordRegister, BlockSizeMode>(22, 3,
                        writeCallback: (_, value) =>
                        {
                            descriptor.blockSize = value;
                            parent.InfoLog("Source burst size {0} ", descriptor.blockSize);
                        },
                        valueProviderCallback: _ => descriptor.blockSize,
                        name: "SRCBURSTSIZE")
                    .WithReservedBits(25, 4)
                    .WithFlag(29, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) => descriptor.priority = value,
                        name: "PRIORITY")
                    .WithFlag(30, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) => descriptor.SRCREQSELB5 = value,
                        name: "SRCREQSELB5")
                    .WithFlag(31, FieldMode.Read | FieldMode.Write,
                        writeCallback: (_, value) => descriptor.DSTREQSELB5 = value,
                        name: "DSTREQSELB5")
                    .WithChangeCallback((_, __) => { if ((descriptor.Enabled) && (LinkStructureAddress == 0)) {Transfer();} 
                                                     if ((descriptor.Enabled) && (LinkStructureAddress != 0)) {LinkLoad();}  })
                ;
                SourceAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.sourceAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.sourceAddress,
                        name: "SRCADDR")
                ;
                DestinationAddressRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 32,
                        writeCallback: (_, value) => descriptor.destinationAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.destinationAddress,
                        name: "DSTADDR")
                ;
                TransferSizeRegister = new DoubleWordRegister(parent)
                    .WithValueField(0, 22,
                        writeCallback: (_, value) => descriptor.TranSize = (uint)value,
                        name: "TRANSIZE")
                    .WithReservedBits(22, 9)
                ;
                LinkListPointerRegister = new DoubleWordRegister(parent)
                    .WithReservedBits(0, 2)
                    .WithValueField(2, 30,
                        writeCallback: (_, value) => descriptor.linkAddress = (uint)value,
                        valueProviderCallback: _ => descriptor.linkAddress,
                        name: "LLPOINTER")
                ;
            }

            public void Reset()
            {
                descriptor = default(Descriptor);
                descriptorAddress = null;
            }

            public bool ChAbort
            {
                get
                {
                    return channelabort;
                }

                set
                {
                    if (value)
                    {
                        channelabort = value;
                        if (channelabort && descriptor.Enabled && (!interruptAbtMask))
                        {
                            descriptor.Enabled = false;
                            descriptor = default(Descriptor);
                            descriptorAddress = null;
                            parent.InfoLog("Channel abort asserted");
                            abortstatus = true;
                        }
                    }

                }
            }

            public int Index { get; }
            public bool interruptTCstatus { get; set; }
            public bool errorstatus { get; set; }
            public bool interruptTCMask => descriptor.interruptTCMask;
            public bool interruptAbtMask => descriptor.InterruptAbtMask;
            public bool interruptErrMask => descriptor.InterruptErrMask;

            public bool IRQ1 => (!interruptTCMask) & interruptTCstatus;
            public bool IRQ2 => (!interruptAbtMask) & abortstatus;
            public bool IRQ3 => (!interruptErrMask) & errorstatus;
            public bool IRQ => IRQ1 || IRQ2 || IRQ3;

            public DoubleWordRegister ConfigurationRegister { get; }
            public DoubleWordRegister ControlRegister { get; }
            public DoubleWordRegister SourceAddressRegister { get; }
            public DoubleWordRegister DestinationAddressRegister { get; }
            public DoubleWordRegister TransferSizeRegister { get; }
            public DoubleWordRegister LinkListPointerRegister { get; }

            public bool ChEN => descriptor.Enabled;
            public bool channelabort;
            public bool abortstatus;

            public void LinkLoad()
            {
                parent.InfoLog("Link Loaded");
                StartTransferInner();
            }

            private void StartTransferInner()
            {
                if (isInProgress)
                {
                    return;
                }

                isInProgress = true;
                var loaded = false;
                do
                {
                    loaded = false;
                    Transfer();
                    if (LinkStructureAddress != 0)
                    {
                        loaded = true;
                        LoadDescriptor();
                    }
                }
                while (loaded);
                isInProgress = false;
            }

            private void LoadDescriptor()
            {
                var address = LinkStructureAddress;
                var data = parent.sysbus.ReadBytes(address, DescriptorSize);
                descriptorAddress = address;
                descriptor = Packet.Decode<Descriptor>(data);
                parent.Log(LogLevel.Info, "Channel #{0} data {1}", Index, BitConverter.ToString(data));
                parent.Log(LogLevel.Info, "Channel #{0} Loaded {1}", Index, descriptor.PrettyString);
            }

            private void Transfer()
            { if(errorstatus || abortstatus)
                {   parent.UpdateInterrupts();
                    return;
                }
                do
                {    
                    var blockSizeMultiplier = Math.Min(TranSize, BlockSizeMultiplier);
                    if (descriptor.SrcAddrCtrl==AddressMode.Decrement)
                    {
                    descriptor.sourceAddress -= SourceIncrement * blockSizeMultiplier;
                    }
                    if (descriptor.DstAddrCtrl==AddressMode.Decrement)
                    {
                    descriptor.destinationAddress -= DestinationIncrement * blockSizeMultiplier;
                    }
                

                    var request = new Request(
                        source: new Place(descriptor.sourceAddress),
                        destination: new Place(descriptor.destinationAddress),
                        size: Bytes,
                        readTransferType: SizeAsTransferType,
                        writeTransferType: SizeAsTransferType,
                        sourceIncrementStep: SourceIncrement,
                        destinationIncrementStep: DestinationIncrement,
                       /* incrementReadAddress:false,  //source increment
                        incrementWriteAddress:true,  //destination increment
                        decrementReadAddress:true,  //source decrement
                        decrementWriteAddress:false  //destination decrement*/

                        incrementReadAddress:SourceInc,  //source increment
                        incrementWriteAddress:DestinationInc,  //destination increment
                        decrementReadAddress:SourceDec,  //source dec
                        decrementWriteAddress:DestinationDec  //destination decrement


                    );
                    
                    parent.Log(LogLevel.Info, "Channel #{0} Performing Transfer", Index);
                    parent.engine.IssueCopy(request); 
                    if (blockSizeMultiplier == TranSize)
                    {
                        descriptor.TranSize = 0;
                        interruptTCstatus = true;
                    }
                    else
                    {
                        descriptor.TranSize -= blockSizeMultiplier;
                    }
                   
                    if ((descriptor.SrcAddrCtrl==AddressMode.Increment) || (descriptor.SrcAddrCtrl==AddressMode.Fixed))
                     {
                        descriptor.sourceAddress += SourceIncrement * blockSizeMultiplier;
                     }
                    if ((descriptor.DstAddrCtrl==AddressMode.Increment) || (descriptor.DstAddrCtrl==AddressMode.Fixed))
                     {
                       descriptor.destinationAddress += DestinationIncrement * blockSizeMultiplier;
                     }
                    
                }
                while (descriptor.TranSize != 0);
                parent.UpdateInterrupts();
            }

            private uint BlockSizeMultiplier
            {
                get
                {
                    switch (descriptor.blockSize)
                    {
                        case BlockSizeMode.Unit1:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit2:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit4:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit8:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit16:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit32:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit64:
                            return 1u << (byte)descriptor.blockSize;
                        case BlockSizeMode.Unit128:
                            return 1u << (byte)descriptor.blockSize;
                        default:
                            parent.Log(LogLevel.Warning, "Channel #{0} Invalid Block Size Mode value.", Index);
                            return 0;
                    }
                }
            }

            private uint TranSize => (uint)descriptor.TranSize;
            private ulong LinkStructureAddress => (ulong)descriptor.linkAddress << 2;
            private uint SourceIncrement => descriptor.SrcAddrCtrl == AddressMode.Fixed ? 0u : ((1u << (byte)descriptor.srcwidth));
            private uint DestinationIncrement => descriptor.DstAddrCtrl == AddressMode.Fixed ? 0u : ((1u << (byte)descriptor.dstwidth));
            private TransferType SizeAsTransferType => (TransferType)(1 << (byte)descriptor.srcwidth);
            private int Bytes => (int)Math.Min(TranSize, BlockSizeMultiplier) << (byte)descriptor.srcwidth;
            private bool SourceMode
            {
               set 
                {
                  if (descriptor.SrcAddrCtrl==AddressMode.Increment) 
                  {
                       SourceInc=true;
                       SourceDec=false;
                  }
                  if (descriptor.SrcAddrCtrl==AddressMode.Decrement) 
                  {
                       SourceInc=false;
                       SourceDec=true;
                  }

                }

            } 
            private bool DestinationMode
            {
               set 
                {
                  if (descriptor.DstAddrCtrl==AddressMode.Increment) 
                  {
                       DestinationInc=true;
                       DestinationDec=false;
                  }
                  if (descriptor.SrcAddrCtrl==AddressMode.Decrement) 
                  {
                       DestinationInc=false;
                       DestinationDec=true;
                  }

                }

            } 

                
           public bool SourceInc;
           public bool SourceDec;

            public bool DestinationInc;
             public bool DestinationDec;
            


           

            private Descriptor descriptor;
            private ulong? descriptorAddress;
            private bool isInProgress;
            private IFlagRegisterField InterruptTCMask;
            private readonly ATCDMAC100 parent;
            protected readonly int DescriptorSize = Packet.CalculateLength<Descriptor>();

            protected enum BlockSizeMode
            {
                Unit1 = 0x0,
                Unit2 = 0x1,
                Unit4 = 0x2,
                Unit8 = 0x3,
                Unit16 = 0x4,
                Unit32 = 0x5,
                Unit64 = 0x6,
                Unit128 = 0x7,

            }

            protected enum AddressMode
            {
                Increment = 0x0,
                Decrement = 0x1,
                Fixed = 0x2,
                Reserved = 0x3,
            }

            protected enum SizeMode
            {
                Byte = 0x0,
                HalfWord = 0x1,
                Word = 0x2,

            }

            [LeastSignificantByteFirst]
            private struct Descriptor
            {
                public string PrettyString => $@"Descriptor {{        
    Enable: {Enabled},
    InterruptTCMask: {interruptTCMask},
    InterruptErrMask: {InterruptErrMask},
    InterruptAbtMask: {InterruptAbtMask},
    DstReqSel: {DstReqSel},
    SrcReqSel: {SrcReqSel},
    DstAddrCtrl: {DstAddrCtrl},
    SrcAddrCtrl: {SrcAddrCtrl},
    DstMode: {DstMode},
    SrcMode: {SrcMode},
    Dstwidth: {dstwidth},
    Srcwidth: {srcwidth},
    Priority:{priority},
    SRCREQSELB5:{SRCREQSELB5},
    DSTREQSELB5:{DSTREQSELB5}
    SrcBurstSize:{blockSize},
    SourceAddress: 0x{sourceAddress:X},
    DestinationAddress: 0x{destinationAddress:X},
    TranSize: {TranSize},
    LinkAddress: 0x{(linkAddress << 2):X}
}}";

                
                // bits : starting position w.r.t register
#pragma warning disable 649
                [PacketField, Offset(doubleWords: 0, bits: 0), Width(1)]
                public bool Enabled;
                [PacketField, Offset(doubleWords: 0, bits: 1), Width(1)]
                public bool interruptTCMask;
                [PacketField, Offset(doubleWords: 0, bits: 2), Width(1)]
                public bool InterruptErrMask;
                [PacketField, Offset(doubleWords: 0, bits: 3), Width(1)]
                public bool InterruptAbtMask;
                [PacketField, Offset(doubleWords: 0, bits: 4), Width(4)]
                public uint DstReqSel;
                [PacketField, Offset(doubleWords: 0, bits: 8), Width(4)]
                public uint SrcReqSel;
                [PacketField, Offset(doubleWords: 0, bits: 12), Width(2)]
                public AddressMode DstAddrCtrl;
                [PacketField, Offset(doubleWords: 0, bits: 14), Width(2)]
                public AddressMode SrcAddrCtrl;
                [PacketField, Offset(doubleWords: 0, bits: 16), Width(1)]
                public bool DstMode;
                [PacketField, Offset(doubleWords: 0, bits: 17), Width(1)]
                public bool SrcMode;
                [PacketField, Offset(doubleWords: 0, bits: 18), Width(2)]
                public SizeMode dstwidth;
                [PacketField, Offset(doubleWords: 0, bits: 20), Width(2)]
                public SizeMode srcwidth;
                [PacketField, Offset(doubleWords: 0, bits: 22), Width(3)]
                public BlockSizeMode blockSize;
                [PacketField, Offset(doubleWords: 0, bits: 29), Width(1)]
                public bool priority;
                [PacketField, Offset(doubleWords: 0, bits: 30), Width(1)]
                public bool SRCREQSELB5;
                [PacketField, Offset(doubleWords: 0, bits: 31), Width(1)]
                public bool DSTREQSELB5;
                [PacketField, Offset(doubleWords: 1, bits: 0), Width(32)]
                public uint sourceAddress;
                [PacketField, Offset(doubleWords: 2, bits: 0), Width(32)]
                public uint destinationAddress;
                [PacketField, Offset(doubleWords: 3, bits: 0), Width(22)]
                public uint TranSize;
                [PacketField, Offset(doubleWords: 4, bits: 2), Width(30)]
                public uint linkAddress;
#pragma warning restore 649
            }
        }
    }
}