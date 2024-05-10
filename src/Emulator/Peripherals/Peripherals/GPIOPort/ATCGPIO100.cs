//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.Bus;
using System;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class ATCGPIO100 : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public ATCGPIO100(Machine machine, bool cfgDebounceSupport = false, bool cfgPullSupport = true, bool cfgIntrSupport = true, int cfgGpioNum = 32) : base(machine, cfgGpioNum)
        {
            this.cfgDebounceSupport = cfgDebounceSupport;
            this.cfgPullSupport = cfgPullSupport;
            this.cfgIntrSupport = cfgIntrSupport;
            this.cfgGpioNum = cfgGpioNum;
            channelInterruptMode = new InterruptMode[cfgGpioNum];

            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }


        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
            for (int i = 0; i < cfgGpioNum; i++)
            {
                channelInterruptMode[i] = InterruptMode.None;
            }
        }


        public override void OnGPIO(int number, bool value)
        {
            // check pin number and verify pin dir is input first
            if (!CheckPinNumber(number) || BitHelper.IsBitSet(channelDirReg, (byte)number))
            {
                return;
            }
          this.InfoLog("ATCGPIO OnGPIO");
            var oldValue = State[number];
            base.OnGPIO(number, value);
            BitHelper.SetBit(ref dataInReg, (byte)number, value);


            // check for interrupt events
            if (BitHelper.IsBitSet(interruptEnReg, (byte)number))
            {

                bool positiveEdge = (!oldValue) && value;
                bool negativeEdge = oldValue && (!value);
                bool dualEdge = positiveEdge || negativeEdge;
                switch (channelInterruptMode[number])
                {
                    case InterruptMode.HighLevel:
                        BitHelper.SetBit(ref interruptStatusReg, (byte)number, value);
                        break;
                    case InterruptMode.LowLevel:
                        BitHelper.SetBit(ref interruptStatusReg, (byte)number, !value);
                        break;
                    case InterruptMode.PositiveEdge:
                        BitHelper.SetBit(ref interruptStatusReg, (byte)number, positiveEdge);
                        break;
                    case InterruptMode.NegativeEdge:
                        BitHelper.SetBit(ref interruptStatusReg, (byte)number, negativeEdge);
                        break;
                    case InterruptMode.DualEdge:
                        BitHelper.SetBit(ref interruptStatusReg, (byte)number, dualEdge);
                        break;
                    default:
                        break;
                }

            }
            UpdateInterrupt();
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }


        private void CheckAllLevelInterruptStatus()
        {
            for (int i = 0; i < cfgGpioNum; i++)
            {
                bool level = BitHelper.IsBitSet(dataInReg, (byte)i);
                bool intrEn = BitHelper.IsBitSet(interruptEnReg, (byte)i);
                InterruptMode mode = channelInterruptMode[i];

                if (intrEn)
                {
                    if (mode == InterruptMode.LowLevel)
                    {
                        BitHelper.SetBit(ref interruptStatusReg, (byte)i, !level);
                    }
                    if (mode == InterruptMode.HighLevel)
                    {
                        BitHelper.SetBit(ref interruptStatusReg, (byte)i, level);
                    }
                }
            }
        }

        public void DefineRegisters()
        {
            Registers.IdRev.Define(this)

            ;

            //Configuration Register ~ 0x10
            Registers.Cfg.Define(this)
                .WithFlag(31, FieldMode.Read, name: "Pull", valueProviderCallback: _ => cfgPullSupport)
                .WithFlag(30, FieldMode.Read, name: "Intr", valueProviderCallback: _ => cfgIntrSupport)
                .WithFlag(29, FieldMode.Read, name: "Debounce", valueProviderCallback: _ => cfgDebounceSupport)
                .WithReservedBits(5, 23)
                .WithValueField(0, 5, FieldMode.Read, name: "ChannelNum", valueProviderCallback: _ => (ulong)cfgGpioNum)
            ;

            // Channel Data-In Register ~ Offset 0x20 - readonly
            Registers.DataIn.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read, name: "DataIn",
                    valueProviderCallback: _ => dataInReg,
                    writeCallback: (_, value) => this.LogUnhandledWrite(0x20, value))
                ;

            // Channel Data-Out Register ~ Offset 0x24
            Registers.DataOut.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "DataOut",
                 valueProviderCallback: _ => dataOutReg,
                 writeCallback: (_, value) =>
                 {
                     dataOutReg = (uint)value;
                     for (var i = 0; i < cfgGpioNum; i++)
                     {
                         UpdateOutputPinState(i);
                     }
                 })
                ;

            // Channel Direction Register ~ Offset 0x28
            Registers.ChannelDir.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "ChannelDir",
                    valueProviderCallback: _ => channelDirReg,
                    writeCallback: (_, value) => { channelDirReg = (uint)value; });

            // Channel Data-Out Clear Register ~ Offset 0x2C
            Registers.DoutClear.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.WriteOneToClear, name: "DoutClear",
                    writeCallback: (_, value) =>
                    {
                        BitHelper.AndWithNot(ref dataOutReg, (uint)value, 0, 32);
                        for (var i = 0; i < cfgGpioNum; i++)
                        {
                            UpdateOutputPinState(i);
                        }
                    });

            // Channel Data-Out Set Register ~ Offset 0x30
            Registers.DoutSet.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Write, name: "DoutSet",
                    writeCallback: (_, value) =>
                    {
                        dataOutReg = dataOutReg & (uint)value;
                        for (var i = 0; i < cfgGpioNum; i++)
                        {
                            UpdateOutputPinState(i);
                        }
                    })
                ;

            // Pull Enable Register ~ Offset 0x40
            Registers.PullEn.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "PullEn",
                    valueProviderCallback: _ => pullEnReg,
                    writeCallback: (_, value) => { pullEnReg = (uint)value; });

            // Pull Type Register ~ Offset 0x44
            Registers.PullType.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "PullType",
                    valueProviderCallback: _ => pullTypeReg,
                    writeCallback: (_, value) => { pullTypeReg = (uint)value; })
                ;

            // Interrupt Enable Register ~ Offset 0x50
            Registers.IntrEn.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "IntEn",
                    valueProviderCallback: _ => interruptEnReg,
                    writeCallback: (_, value) =>
                    {
                        interruptEnReg = (uint)value;
                        CheckAllLevelInterruptStatus();
                        UpdateInterrupt();
                    })
                ;


            // Channel (0~7) Interrupt Mode Register ~ Offset 0x54
            var intrMode0 = Registers.IntrMode0.Define(this);
            for (int i = 0; i < 8; i++)
            {
                var offset = 4 * i;
                var startIndex = i + 0;
                intrMode0 = intrMode0
                .WithValueField(offset, 3, FieldMode.Read | FieldMode.Write, name: $"Ch{startIndex}IntrM",
                    valueProviderCallback: _ => (uint)channelInterruptMode[startIndex],
                    writeCallback: (_, value) =>
                    {
                        channelInterruptMode[startIndex] = (InterruptMode)(uint)value;
                    })
                .WithReservedBits(offset + 3, 1);
            }

            // Channel (8~15) Interrupt Mode Register ~ Offset 0x58
            var intrMode1 = Registers.IntrMode1.Define(this);
            for (int i = 0; i < 8; i++)
            {
                var offset = 4 * i;
                var startIndex = i + 8;
                intrMode1 = intrMode1
                .WithValueField(offset, 3, FieldMode.Read | FieldMode.Write, name: $"Ch{startIndex}IntrM",
                    valueProviderCallback: _ => (uint)channelInterruptMode[startIndex],
                    writeCallback: (_, value) =>
                    {
                        channelInterruptMode[startIndex] = (InterruptMode)(uint)value;
                    })
                .WithReservedBits(offset + 3, 1);
            }

            // Channel (16~23) Interrupt Mode Register ~ Offset 0x5C
            var intrMode2 = Registers.IntrMode2.Define(this);
            for (int i = 0; i < 8; i++)
            {
                var offset = 4 * i;
                var startIndex = i + 16;
                intrMode2 = intrMode2
                .WithValueField(offset, 3, FieldMode.Read | FieldMode.Write, name: $"Ch{startIndex}IntrM",
                    valueProviderCallback: _ => (uint)channelInterruptMode[startIndex],
                    writeCallback: (_, value) =>
                    {
                        channelInterruptMode[startIndex] = (InterruptMode)(uint)value;
                    })
                .WithReservedBits(offset + 3, 1);
            }

            // Channel (24~31) Interrupt Mode Register ~ Offset 0x60
            var intrMode3 = Registers.IntrMode3.Define(this);
            for (int i = 0; i < 8; i++)
            {
                var offset = 4 * i;
                var startIndex = i + 24;
                intrMode3 = intrMode3
                .WithValueField(offset, 3, FieldMode.Read | FieldMode.Write, name: $"Ch{startIndex}IntrM",
                    valueProviderCallback: _ => (uint)channelInterruptMode[startIndex],
                    writeCallback: (_, value) =>
                    {
                        channelInterruptMode[startIndex] = (InterruptMode)(uint)value;
                    })
                .WithReservedBits(offset + 3, 1);
            }

            // Channel Interrupt Status Register ~ Offset 0x64
            Registers.IntrStatus.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.WriteOneToClear, name: "IntrStatus",
                    valueProviderCallback: _ => interruptStatusReg,
                    writeCallback: (_, value) =>
                    {
                        // W1C
                        Console.WriteLine("IntrStatus: value = {0}", value);
                        BitHelper.AndWithNot(ref interruptStatusReg, (uint)value, 0, 32);
                        UpdateInterrupt();
                    })
                ;

            // De-bounce Enable Register ~ Offset 0x70
            Registers.DeBounceEn.Define(this)
                .WithValueField(0, cfgGpioNum, FieldMode.Read | FieldMode.Write, name: "DeBounceEn",
                    valueProviderCallback: _ => deBounceEnReg,
                    writeCallback: (_, value) => { deBounceEnReg = (uint)value; })
                ;

            // De-bounce Control Register ~ Offset 0x74
            Registers.DeBounceCtrl.Define(this)
                .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "DBClkSel",
                    writeCallback: (_, value) => { DebounceClkSelValue = value; },
                    valueProviderCallback: _ => DebounceClkSelValue)
                .WithReservedBits(8, 23)
                .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "DBPreScale",
                    writeCallback: (_, value) => { DebouncePreScaleValue = (uint)value; },
                    valueProviderCallback: _ => DebouncePreScaleValue)
                ;
        }

        private void UpdateInterrupt()
        {

            bool interrupt = BitHelper.AreAnyBitsSet(interruptStatusReg, 0, cfgGpioNum);
            IRQ.Set(interrupt);
        }

        private void UpdateOutputPinState(int pin)
        {
            if ((channelDirReg & (1 << pin)) != 0)
            {
                bool pinState = (dataOutReg & (1 << pin)) != 0;
                Connections[pin].Set(pinState);
            }
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }
        public GPIO IRQ { get; set; }
        public long Size => 0x84;

        public uint dataInReg = 0x0;
        private uint dataOutReg = 0x0;
        private uint channelDirReg = 0x0;
        private uint pullEnReg = 0x0; 
        private uint pullTypeReg = 0x0; // dummy, Renode does not support tristate pins
        private uint interruptEnReg = 0x0;
        private readonly InterruptMode[] channelInterruptMode; 
        private uint interruptStatusReg = 0x0;
        private uint deBounceEnReg = 0x0; // dummy
        private bool DebounceClkSelValue = false;  // dummy
        private uint DebouncePreScaleValue = 0x0;  // dummy

        //Configuration constants:
        private readonly int cfgGpioNum; //number of gpio channels (pins)
        private readonly bool cfgPullSupport; //Pull option IS NOT configured
        private readonly bool cfgIntrSupport; //interrupt option IS configured
        private readonly bool cfgDebounceSupport; //de-bounce option IS NOT configured

        private enum Registers : long
        {
            IdRev = 0x00,       // ID and revision register
                                // Reserved = 0x04 - 0x0C
            Cfg = 0x10,         // Configuration register
                                // Reserved = 0x14 - 0x1C
            DataIn = 0x20,      // Channel data-in register
            DataOut = 0x24,     // Channel data-out register
            ChannelDir = 0x28,  // Channel direction register
            DoutClear = 0x2C,   // Channel data-out clear register
            DoutSet = 0x30,     // Channel data-out set register
                                // Reserved = 0x34 - 0x3C
            PullEn = 0x40,      // Pull enable register
            PullType = 0x44,    // Pull type register
                                // Reserved = 0x48 - 0x4C
            IntrEn = 0x50,      // Interrupt enable register
            IntrMode0 = 0x54,   // Interrupt mode register (0~7)
            IntrMode1 = 0x58,   // Interrupt mode register (8~15)
            IntrMode2 = 0x5C,   // Interrupt mode register (16~23)
            IntrMode3 = 0x60,   // Interrupt mode register (24~31)
            IntrStatus = 0x64,  // Interrupt status register
                                // Reserved = 0x68 - 0x6C
            DeBounceEn = 0x70,  // De-bounce enable register
            DeBounceCtrl = 0x74 // De-bounce control register
                                // Reserved = 0x78 - 0x7C            
        }

        private enum InterruptMode : uint
        {
            None = 0x0,
            // Reserved = 0x1,
            HighLevel = 0x2,
            LowLevel = 0x3,
            // Reserved = 0x4,
            NegativeEdge = 0x5,
            PositiveEdge = 0x6,
            DualEdge = 0x7
        }
    }
}
