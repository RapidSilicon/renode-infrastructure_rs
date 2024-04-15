//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Peripherals.DMA
{
    public struct Request
    {
        public Request(Place source, Place destination, int size, TransferType readTransferType, TransferType writeTransferType,
            bool incrementReadAddress = true, bool incrementWriteAddress = true) : this()
        {
            this.Source = source;
            this.Destination = destination;
            this.Size = size;
            this.ReadTransferType = readTransferType;
            this.WriteTransferType = writeTransferType;
            this.IncrementReadAddress = incrementReadAddress;
            this.IncrementWriteAddress = incrementWriteAddress;
            this.SourceIncrementStep = (uint)readTransferType;
            this.DestinationIncrementStep = (uint)writeTransferType;
        }

        public Request(Place source, Place destination, int size, TransferType readTransferType, TransferType writeTransferType, 
            uint sourceIncrementStep, uint destinationIncrementStep, bool incrementReadAddress = true, 
            bool incrementWriteAddress = true) : this()
        {
            this.Source = source;
            this.Destination = destination;
            this.Size = size;
            this.ReadTransferType = readTransferType;
            this.WriteTransferType = writeTransferType;
            this.IncrementReadAddress = incrementReadAddress;
            this.IncrementWriteAddress = incrementWriteAddress;
            this.SourceIncrementStep = sourceIncrementStep;
            this.DestinationIncrementStep = destinationIncrementStep;
        }
       //Add new Constructor for increment / decrement addressing modes of atcdmac
        public Request(Place source, Place destination, int size, TransferType readTransferType, TransferType writeTransferType, 
            uint sourceIncrementStep, uint destinationIncrementStep, bool incrementReadAddress, 
            bool incrementWriteAddress, bool decrementReadAddress, bool decrementWriteAddress) : this()
        {
            this.Source = source;
            this.Destination = destination;
            this.Size = size;
            this.ReadTransferType = readTransferType;
            this.WriteTransferType = writeTransferType;
            this.SourceIncrementStep = sourceIncrementStep;
            this.DestinationIncrementStep = destinationIncrementStep;
            this.IncrementReadAddress = incrementReadAddress;
            this.IncrementWriteAddress = incrementWriteAddress;
            this.DecrementReadAddress = decrementReadAddress;
            this.DecrementWriteAddress = decrementWriteAddress;
        }

        public Place Source { get; private set; }
        public Place Destination { get; private set; }
        public uint SourceIncrementStep { get; private set; }
        public uint DestinationIncrementStep { get; private set; }
        public int Size { get; private set; }
        public TransferType ReadTransferType { get; private set; }
        public TransferType WriteTransferType { get; private set; }
        public bool IncrementReadAddress { get; private set; }
        public bool IncrementWriteAddress { get; private set; }
        public bool DecrementReadAddress { get; private set; }
        public bool DecrementWriteAddress { get; private set; }


    }
}
