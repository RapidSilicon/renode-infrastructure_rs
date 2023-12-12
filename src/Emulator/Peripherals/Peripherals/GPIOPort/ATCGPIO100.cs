//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class ATCGPIO100 : BaseGPIOPort, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IDoubleWordPeripheral, IKnownSize
    {
        public ATCGPIO100(Machine machine) : base(machine, NumberOfPins)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);

            DefineRegisters();
            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();

            outputPinValues.Initialize();
            tristatePinOutputEnabled.Initialize();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            var oldState = State[number];
            base.OnGPIO(number, value);

            if(inputEnable[number].Value && oldState != value)
            {
                HandlePinStateChangeInterrupt(number, risingEdge: value);
            }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x440;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.IdRev.Define(this)

            ;

            //Configuration Register ~ 0x10
            Registers.Cfg.Define(this)
                .WithFlag(31, FieldMode.Read, name: "Pull")
                .WithFlag(30, FieldMode.Read, name: "Intr")
                .WithFlag(29, FieldMode.Read, name: "Debounce")
                .WithReservedBits(5,23)
                .WithValueField(0, 5, FieldMode.Read, name:"ChannelNum",
                    valueProviderCallback: _ => 0) //todo
            ;

            // Channel Data-In Register ~ Offset 0x20
            Registers.DataIn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read, name: "DataIn", 
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel Data-Out Register ~ Offset 0x24
            Registers.DataOut.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "DataOut", 
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel Direction Register ~ Offset 0x28
            Registers.ChannelDir.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "ChannelDir", 
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel Data-Out Clear Register ~ Offset 0x2C
            Registers.DoutClear.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Write, name: "DoutClear", 
                    valueProviderCallback: _ => 0x0)
                ;
            // Channel Data-Out Set Register ~ Offset 0x30
            Registers.DoutSet.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Write, name: "DoutSet",
                    valueProviderCallback: _ => 0x0)
                ;

            // Pull Enable Register ~ Offset 0x40
            Registers.PullEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "PullEn",
                    valueProviderCallback: _ => 0x0)
                ;

            // Pull Type Register ~ Offset 0x44
            Registers.PullType.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "PullType",
                    valueProviderCallback: _ => 0x0)
                ;

            // Interrupt Enable Register ~ Offset 0x50
            Registers.IntrEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "IntEn",
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel (0~7) Interrupt Mode Register ~ Offset 0x54
            Registers.IntrMode0.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch7IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch6IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch5IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch4IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch3IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch2IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch1IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch0IntrM",
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel (8~15) Interrupt Mode Register ~ Offset 0x58
            Registers.IntrMode1.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch15IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch14IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch13IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch12IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch11IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch10IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch9IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch8IntrM",
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel (16~23) Interrupt Mode Register ~ Offset 0x5C
            Registers.IntrMode2.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch23IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch22IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch21IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch20IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch19IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch18IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch17IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch16IntrM",
                    valueProviderCallback: _ => 0x0)
                ;

            // Channel (24~31) Interrupt Mode Register ~ Offset 0x60
            Registers.IntrMode3.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch31IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch30IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch29IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch28IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch27IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch26IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch25IntrM",
                    valueProviderCallback: _ => 0x0)
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch24IntrM",
                    valueProviderCallback: _ => 0x0)
                ;


            // Channel Interrupt Status Register ~ Offset 0x64
            Registers.IntrStatus.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write | FieldMode.WriteOneToClear, name: "IntrStatus",
                    valueProviderCallback: _ => 0x0)

                ;

            // De-bounce Enable Register ~ Offset 0x70
            Registers.DeBounceEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "DeBounceEn",
                    valueProviderCallback: _ => 0x0)

                ;

            // De-bounce Control Register ~ Offset 0x74
            Registers.DeBounceCtrl.Define(this)
                .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "DBClkSel",
                    valueProviderCallback: _ => false)
                .WithReservedBits(8, 23)
                .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "DBPreScale",
                    valueProviderCallback: _ => 0x0)
                ;

        }


        private void HandlePinStateChangeInterrupt(int pinIdx, bool risingEdge)
        {
            var mode = interruptMode[pinIdx].Value;
            if(mode == InterruptEnable.EnabledOnAnyTransition
                || (risingEdge && mode == InterruptEnable.EnabledOnRisingEdgeTransition)
                || (!risingEdge && mode == InterruptEnable.EnabledOnFallingEdgeTransition))
            {
                this.Log(LogLevel.Noisy, "Triggering IRQ #{0} on the {1} edge", pinIdx, risingEdge ? "rising" : "falling");
                TriggerInterrupt(pinIdx);
            }
        }

        private bool IsPinOutputEnabled(int pinIdx)
        {
            switch(ioMode[pinIdx].Value)
            {
                case IOMode.OutputDisabled:
                    return false;

                case IOMode.PushPullOutputMode:
                    return true;

                case IOMode.OpenDrainOutputMode:
                    return false;

                case IOMode.TristatePushPullOutputMode:
                    return tristatePinOutputEnabled[pinIdx];

                default:
                    throw new ArgumentException($"Unexpected IOMode: {ioMode[pinIdx].Value}");
            }
        }

        private void SetOutputPinValue(int pinIdx, bool state)
        {
            outputPinValues[pinIdx] = state;
            UpdateOutputPinState(pinIdx);
        }

        private void TriggerInterrupt(int pinIdx)
        {
            var irqBank = pinIdx / PinsPerBank;
            var banksPinOffset = pinIdx % PinsPerBank;

            TriggerInterruptInner(irqBank, banksPinOffset);
            TriggerInterruptInner(irqBank, banksPinOffset, n1Priority: true);
        }

        private void TriggerInterruptInner(int irqBank, int banksPinOffset, bool n1Priority = false)
        {
            
        }

        private void UpdateInterrupt()
        {
            
        }

        private void UpdateOutputPinState(int pinIdx)
        {
            
        }

        private IValueRegisterField padKey;

        private readonly IFlagRegisterField[] inputEnable = new IFlagRegisterField[NumberOfPins];
        private readonly IEnumRegisterField<InterruptEnable>[] interruptMode = new IEnumRegisterField<InterruptEnable>[NumberOfPins];
        private readonly IEnumRegisterField<IOMode>[] ioMode = new IEnumRegisterField<IOMode>[NumberOfPins];
        private readonly IFlagRegisterField[][] irqEnabled = new IFlagRegisterField[NumberOfExternalInterrupts][];
        private readonly IFlagRegisterField[][] irqStatus = new IFlagRegisterField[NumberOfExternalInterrupts][];
        private readonly IFlagRegisterField[] readZero = new IFlagRegisterField[NumberOfPins];

        private readonly GPIO[] irq = new [] { new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO(), new GPIO() };
        private readonly bool[] outputPinValues = new bool[NumberOfPins];
        private readonly bool[] tristatePinOutputEnabled = new bool[NumberOfPins];

        private const int FirstVirtualPinIndex = 105;
        private const int NumberOfBanks = NumberOfPins / PinsPerBank;
        private const int NumberOfExternalInterrupts = 8;
        private const int NumberOfPins = 128;
        private const uint PadKeyUnlockValue = 0x73;
        private const int PinsPerBank = 32;

        private const int ATCGPIO100_GPIO_NUM = 32; //number of gpio channels (pins)

        private enum DriveStrength
        {
            OutputDriver0_1x = 0x0, // 0.1x output driver selected
            OutputDriver0_5x = 0x1, // 0.5x output driver selected
        }

        private enum InterruptEnable
        {
            Disabled = 0x0, // Interrupts are disabled for this GPIO
            EnabledOnFallingEdgeTransition = 0x1, // Interrupts are enabled for falling edge transition on this GPIO
            EnabledOnRisingEdgeTransition = 0x2, // Interrupts are enabled for rising edge transitions on this GPIO
            EnabledOnAnyTransition = 0x3, // Interrupts are enabled for any edge transition on this GPIO
        }

        private enum IOMode
        {
            OutputDisabled = 0x0, // Output Disabled
            PushPullOutputMode = 0x1, // Output configured in push pull mode. Will drive 0 and 1 values on pin.
            OpenDrainOutputMode = 0x2, // Output configured in open drain mode. Will only drive pin low, tristate otherwise.
            TristatePushPullOutputMode = 0x3, // Output configured in Tristate-able push pull mode. Will drive 0, 1 of HiZ on pin.
        }

        private enum PolarityConfiguration
        {
            ActiveLow = 0x0, // Polarity is active low
            ActiveHigh = 0x1, // Polarity is active high
        }

        private enum PullUpDownConfiguration
        {
            None = 0x0, // No pullup or pulldown selected
            Pulldown50K = 0x1, // 50K Pulldown selected
            Pullup1_5K = 0x2, // 1.5K Pullup selected
            Pullup6K = 0x3, // 6K Pullup selected
            Pullup12K = 0x4, // 12K Pullup selected
            Pullup24K = 0x5, // 24K Pullup selected
            Pullup50K = 0x6, // 50K Pullup selected
            Pullup100K = 0x7, // 100K Pullup selected
        }

        private enum Registers : long
        {
            IdRev = 0x00, // ID and revision register
            // Reserved = 0x04
            // Reserved = 0x08
            // Reserved = 0x0C
            Cfg = 0x10, // Configuration register
            // Reserved = 0x14
            // Reserved = 0x18
            // Reserved = 0x1C
            DataIn = 0x20, // Channel data-in register
            DataOut = 0x24, // Channel data-out register
            ChannelDir = 0x28, // Channel direction register
            DoutClear = 0x2C, // Channel data-out clear register
            DoutSet = 0x30, // Channel data-out set register
            // Reserved = 0x34
            // Reserved = 0x38
            // Reserved = 0x3C
            PullEn = 0x40, // Pull enable register
            PullType = 0x44, // Pull type register
            // Reserved = 0x48
            // Reserved = 0x4C
            IntrEn = 0x50, // Interrupt enable register
            IntrMode0 = 0x54, // Interrupt mode register (0~7)
            IntrMode1 = 0x58, // Interrupt mode register (8~15)
            IntrMode2 = 0x5C, // Interrupt mode register (16~23)
            IntrMode3 = 0x60, // Interrupt mode register (24~31)
            IntrStatus = 0x64, // Interrupt status register
            // Reserved = 0x68
            // Reserved = 0x6C
            DeBounceEn = 0x70, // De-bounce enable register
            DeBounceCtrl = 0x74, // De-bounce control register
            // Reserved = 0x78
            // Reserved = 0x7C            
        }
    }
}
