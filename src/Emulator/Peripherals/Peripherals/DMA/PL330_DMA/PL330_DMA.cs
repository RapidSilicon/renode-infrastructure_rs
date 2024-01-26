//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.DMA
{
    public partial class PL330_DMA : BasicDoubleWordPeripheral, IKnownSize, IDMA
    {
        // This model doesn't take into account differences in AXI bus width, 
        // which could have impact on unaligned transfers in real HW
        // this is a know limitation at this moment
        public PL330_DMA(IMachine machine) : base(machine)
        {
            channels = new Channel[NumberOfChannels];

            RegisterInstructions();
            DefineRegisters();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();

            for(int i = 0; i < channels.Length; ++i)
            {
                channels[i] = new Channel(this, i);
                channels[i].DefineRegisters();
            }
        }

        public void RequestTransfer(int channel)
        {
            throw new RecoverableException("This DMA requires an in-memory program to transfer data");
        }

        // This method should be called from Monitor to decode instruction at given address.
        // It is intended as a helper to investigate in-memory program.
        // Uses QuadWord accesses, so the target must support them.
        public string TryDecodeInstructionAtAddress(ulong address, bool fullDecode = false)
        {
            ulong bytes = machine.GetSystemBus(this).ReadQuadWord(address);
            if(!decoderRoot.TryParseOpcode((byte)bytes, out var instruction))
            {
                throw new RecoverableException("Unrecognized instruction");
            }
            if(fullDecode)
            {
                instruction.ParseAll(bytes);
            }
            return instruction.ToString();
        }

        public long Size => 0x1000;

        public int NumberOfChannels => 8;

        public byte Revision { get; set; } = 0x3;

        private void DefineRegisters()
        {
            Registers.DebugCommand.Define(this)
                .WithValueField(0, 2, FieldMode.Write,
                    writeCallback: (_, val) =>
                    {
                        if(val == 0b00)
                        {
                            ExecuteDebugStart();
                        }
                        else
                        {
                            this.Log(LogLevel.Error, "Undefined DMA Debug Command: {0}", val);
                        }
                    },
                    name: "Debug Command")
                .WithReservedBits(2, 30);

            Registers.DebugInstruction0.Define(this)
                .WithEnumField(0, 1, out debugThreadType, name: "Debug thread select")
                .WithReservedBits(1, 7)
                .WithValueField(8, 3, out debugChannelNumber, name: "Debug channel select")
                .WithReservedBits(11, 5)
                .WithValueField(16, 8, out debugInstructionByte0, name: "Instruction byte 0")
                .WithValueField(24, 8, out debugInstructionByte1, name: "Instruction byte 1");

            Registers.DebugInstruction1.Define(this)
                .WithValueField(0, 32, out debugInstructionByte2_3_4_5, name: "Instruction byte 2,3,4,5");

            Registers.PeripheralIdentification0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x30, name: "Part number 0")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification1.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x3, name: "Part number 1")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => 0x1, name: "Designer 0")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification2.Define(this)
                .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => 0x4, name: "Designer 1")
                .WithValueField(4, 4, FieldMode.Read, valueProviderCallback: _ => Revision, name: "Revision")
                .WithReservedBits(8, 24);

            Registers.PeripheralIdentification3.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => false, name: "Integration test logic")
                .WithReservedBits(1, 31);

            Registers.ComponentIdentification0.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x0D, name: "Component ID 0")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification1.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xF0, name: "Component ID 1")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification2.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0x05, name: "Component ID 2")
                .WithReservedBits(8, 24);

            Registers.ComponentIdentification3.Define(this)
                .WithValueField(0, 8, FieldMode.Read, valueProviderCallback: _ => 0xB1, name: "Component ID 3")
                .WithReservedBits(8, 24);
        }

        private void ExecuteDebugStart()
        {
            ulong debugInstructionBytes = debugInstructionByte2_3_4_5.Value << 16 | debugInstructionByte1.Value << 8 | debugInstructionByte0.Value;

            string binaryInstructionString = Convert.ToString((long)debugInstructionBytes, 2);
            this.Log(LogLevel.Debug, "Inserted debug instruction: {0}", binaryInstructionString);

            if(!decoderRoot.TryParseOpcode((byte)debugInstructionBytes, out var debugInstruction))
            {
                this.Log(LogLevel.Error, "Debug instruction \"{0}\" is not supported or invalid. DMA will not execute.", binaryInstructionString);
                return;
            }

            debugInstruction.ParseAll(debugInstructionBytes);
            ExecuteDebugInstruction(debugInstruction, (int)debugChannelNumber.Value, debugThreadType.Value);
        }

        private void ExecuteDebugInstruction(Instruction firstInstruction, int channelIndex, DMAThreadType threadType)
        {
            if(threadType != DMAThreadType.Manager)
            {
                this.Log(LogLevel.Error, "Issuing debug instructions to channel thread is currently unsupported.");
                return;
            }

            if(!(firstInstruction is DMAGO
                || firstInstruction is DMAKILL
                || firstInstruction is DMASEV))
            {
                this.Log(LogLevel.Error, "Debug instruction \"{0}\" is not DMAGO, DMAKILL, DMASEV. It cannot be the first instruction.", firstInstruction.ToString());
                return;
            }

            LogInstructionExecuted(firstInstruction);
            // This is an instruction provided by the debug registers - it can't advance PC
            firstInstruction.Execute(threadType, threadType != DMAThreadType.Manager ? (int?)channelIndex : null, suppressAdvance: true);
            ExecuteLoop();
        }

        private void ExecuteLoop()
        {
            // TODO: in case of infinite loop, this will hang the emulation.
            // It's not ideal - separate thread will be good, but what about time flow?
            // Still, it's enough in the beginning, for simple use cases
            var currentCPU = GetCurrentCPUOrNull();

            foreach(var channelThread in channels.Where(c => c.Status == Channel.ChannelStatus.Executing))
            {
                this.Log(LogLevel.Debug, "Executing channel thread: {0}", channelThread.Id);

                while(channelThread.Status == Channel.ChannelStatus.Executing)
                {
                    var address = channelThread.PC;
                    var insn = machine.GetSystemBus(this).ReadByte(address, context: currentCPU);
                    if(!decoderRoot.TryParseOpcode(insn, out var instruction))
                    {
                        this.Log(LogLevel.Error, "Invalid instruction at address: 0x{0:X}. Stopping thread {1}.", address, channelThread.Id);
                        channelThread.Status = Channel.ChannelStatus.Stopped;
                        continue;
                    }

                    while(!instruction.IsFinished)
                    {
                        instruction.Parse(machine.GetSystemBus(this).ReadByte(address, context: currentCPU));
                        address += sizeof(byte);
                    }

                    LogInstructionExecuted(instruction, channelThread.PC);
                    instruction.Execute(DMAThreadType.Channel, channelThread.Id);
                }
            }
        }

        private void LogInstructionExecuted(Instruction insn, ulong? address = null)
        {
            this.Log(LogLevel.Noisy, "Executing: {0} {1}", insn.ToString(), address != null ? $"@ 0x{address:X}" : "" );
        }

        private ICPU GetCurrentCPUOrNull()
        {
            if(!machine.SystemBus.TryGetCurrentCPU(out var cpu))
            {
                return null;
            }
            return cpu;
        }

        private IEnumRegisterField<DMAThreadType> debugThreadType;
        private IValueRegisterField debugChannelNumber;
        private IValueRegisterField debugInstructionByte0;
        private IValueRegisterField debugInstructionByte1;
        private IValueRegisterField debugInstructionByte2_3_4_5;

        private readonly Channel[] channels;

        private class Channel
        {
            public Channel(PL330_DMA parent, int id)
            {
                this.Parent = parent;
                this.Id = id;
            }

            public void DefineRegisters()
            {
                (Registers.Channel0Status + Id * 8).Define(Parent)
                    .WithValueField(0, 4, FieldMode.Read, valueProviderCallback: _ => (ulong)Status, name: $"Channel {Id} status")
                    .WithTag("Wakeup number", 4, 5)
                    .WithReservedBits(9, 5)
                    .WithTaggedFlag("DMAWFP single/burst", 14)
                    .WithTaggedFlag("DMAWFP is periph set", 15)
                    .WithReservedBits(16, 5)
                    .WithTaggedFlag("Channel Non Secure", 21)
                    .WithReservedBits(22, 10);

                (Registers.Channel0ProgramCounter + Id * 8).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => PC, name: $"Channel {Id} Program Counter");

                (Registers.Channel0SourceAddress + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => SourceAddress, name: $"Channel {Id} Source Address");

                (Registers.Channel0DestinationAddress + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => DestinationAddress, name: $"Channel {Id} Destination Address");

                ChannelControlRawValue = 0x00800200;
                (Registers.Channel0Control + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => ChannelControlRawValue, name: $"Channel {Id} Control");

                (Registers.Channel0LoopCounter0 + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => LoopCounter[0], name: $"Channel {Id} Loop Counter 0");

                (Registers.Channel0LoopCounter1 + Id * 0x20).Define(Parent)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => LoopCounter[1], name: $"Channel {Id} Loop Counter 1");
            }

            public ulong PC { get; set; }

            public uint SourceAddress { get; set; }
            public uint DestinationAddress { get; set; }
            public uint ChannelControlRawValue
            {
                get => channelControlRawValue;
                set
                {
                    channelControlRawValue = value;

                    SourceIncrementingAddress = BitHelper.GetValue(value, 0, 1) == 1;
                    SourceReadSize = 1 << (int)BitHelper.GetValue(value, 1, 3);
                    SourceBurstLength = (int)BitHelper.GetValue(value, 4, 4) + 1;

                    DestinationIncrementingAddress = BitHelper.GetValue(value, 14, 1) == 1;
                    DestinationWriteSize = 1 << (int)BitHelper.GetValue(value, 15, 3);
                    DestinationBurstLength = (int)BitHelper.GetValue(value, 18, 4) + 1;
                }
            }

            public ChannelStatus Status { get; set; } = ChannelStatus.Stopped;

            // RequestType is part of Peripheral Request Interface (is set by `DMAWFP`)
            public ChannelRequestType RequestType { get; set; } = ChannelRequestType.Single;
            // Whether it's a last request - this is set in peripheral transfers only, in infinite loop transfers
            public bool RequestLast { get; set; } = false;

            // Sizes are specified in bytes
            public int SourceReadSize { get; private set; }
            public int DestinationWriteSize { get; private set; }

            public int SourceBurstLength { get; private set; }
            public int DestinationBurstLength { get; private set; }

            public bool SourceIncrementingAddress { get; private set; }
            public bool DestinationIncrementingAddress { get; private set; }

            public readonly int Id;
            public readonly byte[] LoopCounter = new byte[2];
            public readonly Queue<byte> localMFIFO = new Queue<byte>();

            private uint channelControlRawValue;
            private readonly PL330_DMA Parent;

            public enum ChannelStatus
            {
                // The documentation enumerates more states, but let's reduce this number for now
                // for the implementation to be more manageable.
                // Also, some states have no meaning for us, as they are related to the operation of the bus
                Stopped = 0b0000,
                Executing = 0b0001,
                WaitingForEvent = 0b0100,
            }

            public enum ChannelRequestType
            {
                Single = 0,
                Burst = 1
            }
        }

        private enum DMAThreadType
        {
            Manager = 0,
            Channel = 1
        }

        private enum Registers : long
        {
            DmaManagerStatus = 0x0,
            DmaProgramCounter = 0x4,
            DmaInterruptEnable = 0x20,
            DmaEventInterruptRawStatus = 0x24,
            DmaInterruptStatus = 0x28,
            DmaInterruptClear = 0x2C,
            FaultStatusDmaManager = 0x30,
            FaultStatusDmaChannel = 0x34,
            FaultTypeDmaManager = 0x38,

            Channel0FaultType = 0x40,
            Channel1FaultType = 0x44,
            Channel2FaultType = 0x48,
            Channel3FaultType = 0x4C,
            Channel4FaultType = 0x50,
            Channel5FaultType = 0x54,
            Channel6FaultType = 0x58,
            Channel7FaultType = 0x5C,

            Channel0Status = 0x100,
            Channel1Status = 0x108,
            Channel2Status = 0x110,
            Channel3Status = 0x118,
            Channel4Status = 0x120,
            Channel5Status = 0x128,
            Channel6Status = 0x130,
            Channel7Status = 0x138,

            Channel0ProgramCounter = 0x104,
            Channel1ProgramCounter = 0x10C,
            Channel2ProgramCounter = 0x114,
            Channel3ProgramCounter = 0x11C,
            Channel4ProgramCounter = 0x124,
            Channel5ProgramCounter = 0x12C,
            Channel6ProgramCounter = 0x134,
            Channel7ProgramCounter = 0x13C,

            Channel0SourceAddress = 0x400,
            Channel1SourceAddress = 0x420,
            Channel2SourceAddress = 0x440,
            Channel3SourceAddress = 0x460,
            Channel4SourceAddress = 0x480,
            Channel5SourceAddress = 0x4A0,
            Channel6SourceAddress = 0x4C0,
            Channel7SourceAddress = 0x4E0,

            Channel0DestinationAddress = 0x404,
            Channel1DestinationAddress = 0x424,
            Channel2DestinationAddress = 0x444,
            Channel3DestinationAddress = 0x464,
            Channel4DestinationAddress = 0x484,
            Channel5DestinationAddress = 0x4A4,
            Channel6DestinationAddress = 0x4C4,
            Channel7DestinationAddress = 0x4E4,

            Channel0Control = 0x408,
            Channel1Control = 0x428,
            Channel2Control = 0x448,
            Channel3Control = 0x468,
            Channel4Control = 0x488,
            Channel5Control = 0x4A8,
            Channel6Control = 0x4C8,
            Channel7Control = 0x4E8,

            Channel0LoopCounter0 = 0x40C,
            Channel1LoopCounter0 = 0x42C,
            Channel2LoopCounter0 = 0x44C,
            Channel3LoopCounter0 = 0x46C,
            Channel4LoopCounter0 = 0x48C,
            Channel5LoopCounter0 = 0x4AC,
            Channel6LoopCounter0 = 0x4CC,
            Channel7LoopCounter0 = 0x4EC,

            Channel0LoopCounter1 = 0x410,
            Channel1LoopCounter1 = 0x430,
            Channel2LoopCounter1 = 0x450,
            Channel3LoopCounter1 = 0x470,
            Channel4LoopCounter1 = 0x490,
            Channel5LoopCounter1 = 0x4B0,
            Channel6LoopCounter1 = 0x4D0,
            Channel7LoopCounter1 = 0x4F0,

            DebugStatus = 0xD00,
            DebugCommand = 0xD04,
            DebugInstruction0 = 0xD08,
            DebugInstruction1 = 0xD0C,

            Configuration0 = 0xE00,
            Configuration1 = 0xE04,
            Configuration2 = 0xE08,
            Configuration3 = 0xE0C,
            Configuration4 = 0xE10,
            DmaConfiguration = 0xE14,
            Watchdog = 0xE80,

            PeripheralIdentification0 = 0xFE0,
            PeripheralIdentification1 = 0xFE4,
            PeripheralIdentification2 = 0xFE8,
            PeripheralIdentification3 = 0xFEC,
            ComponentIdentification0 = 0xFF0,
            ComponentIdentification1 = 0xFF4,
            ComponentIdentification2 = 0xFF8,
            ComponentIdentification3 = 0xFFC,
        }
    }
}
