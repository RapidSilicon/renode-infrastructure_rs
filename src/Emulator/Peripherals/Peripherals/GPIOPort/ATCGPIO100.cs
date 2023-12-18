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
        public ATCGPIO100(Machine machine) : base(machine, ATCGPIO100_GPIO_NUM)
        {
            RegistersCollection = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();

            DefineRegisters();
            Reset();
        }

        public GPIO IRQ { get; set; }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public override void OnGPIO(int number, bool value)
        {
            if(!CheckPinNumber(number))
            {
                return;
            }

            var oldState = State[number];
            base.OnGPIO(number, value);

            // if(inputEnable[number].Value && oldState != value)
            // {
            //     HandlePinStateChangeInterrupt(number, risingEdge: value);
            // }
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public long Size => 0x84;

        public DoubleWordRegisterCollection RegistersCollection { get; }

        private void editArray(bool[] array, int value){
            //this.Log(LogLevel.Info, "editArray; value = {0}", value);
            for (var i = 0; i < array.Length; i++){
                array[i] = Convert.ToBoolean((value >> i) & 0x1);
                //this.Log(LogLevel.Info, "editArray: array[{0}] = {1}", i, array[i]);
            }
        }

        private uint ReturnArrayAsDoubleWord(bool[] array){
            uint value = 0;
            for (var i = 0; i < array.Length; i++){
                value += Convert.ToUInt32(array[i]) * (uint)Math.Pow(2, i);
                //this.Log(LogLevel.Info, "ReturnArrayAsDoubleWord: value = {0}", value);
            }
            return value;
        }

        private void DefineRegisters()
        {
            Registers.IdRev.Define(this)

            ;

            //Configuration Register ~ 0x10
            Registers.Cfg.Define(this)
                .WithFlag(31, FieldMode.Read, name: "Pull", valueProviderCallback: _ => ATCGPIO100_PULL_SUPPORT) 
                .WithFlag(30, FieldMode.Read, name: "Intr", valueProviderCallback: _ => ATCGPIO100_INTR_SUPPORT) 
                .WithFlag(29, FieldMode.Read, name: "Debounce", valueProviderCallback: _ => ATCGPIO100_DEBOUNCE_SUPPORT)
                .WithReservedBits(5,23)
                .WithValueField(0, 5, FieldMode.Read, name:"ChannelNum",
                    valueProviderCallback: _ => ATCGPIO100_GPIO_NUM)
            ;

            // Channel Data-In Register ~ Offset 0x20 - readonly
            Registers.DataIn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read, name: "DataIn", 
                 valueProviderCallback: _ => ReturnArrayAsDoubleWord(dataInReg),
                 writeCallback: (_, value) => this.LogUnhandledWrite(0x20, value))
            ;

            // Channel Data-Out Register ~ Offset 0x24
            Registers.DataOut.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "DataOut", 
                 valueProviderCallback: _ => ReturnArrayAsDoubleWord(dataOutReg), 
                 writeCallback: (_, value) => {
                        for (var i = 0; i < dataOutReg.Length; i++){
                            if (channelDirReg[i] == true){ //if channelDir is an output
                                dataOutReg[i] = Convert.ToBoolean((value >> i) & 0x1);
                            }
                            
                        }
                    })
                ;

            // Channel Direction Register ~ Offset 0x28
            Registers.ChannelDir.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "ChannelDir", 
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(channelDirReg), 
                    writeCallback: (_, value) => editArray(channelDirReg, (int)value))
                ;

            // Channel Data-Out Clear Register ~ Offset 0x2C
            Registers.DoutClear.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.WriteOneToClear, name: "DoutClear", 
                    writeCallback: (_, value) => {
                        for (var i = 0; i < dataOutReg.Length; i++){
                            if (Convert.ToBoolean((value >> i) & 0x1) == true){
                                dataOutReg[i] = !Convert.ToBoolean((value >> i) & 0x1);
                            }
                            
                        }
                    })
                ;

            // Channel Data-Out Set Register ~ Offset 0x30
            Registers.DoutSet.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Write, name: "DoutSet",
                    writeCallback: (_, value) => {
                        for (var i = 0; i < dataOutReg.Length; i++){
                            if (Convert.ToBoolean((value >> i) & 0x1) == true){
                                dataOutReg[i] = Convert.ToBoolean((value >> i) & 0x1);
                            }
                        }
                    })
                ;

            // Pull Enable Register ~ Offset 0x40
            Registers.PullEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "PullEn",
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(pullEnReg), 
                    writeCallback: (_, value) => editArray(pullEnReg, (int)value))
                ;

            // Pull Type Register ~ Offset 0x44
            Registers.PullType.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "PullType",
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(pullTypeReg), 
                    writeCallback: (_, value) => editArray(pullTypeReg, (int)value))
                ;

            // Interrupt Enable Register ~ Offset 0x50
            Registers.IntrEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "IntEn",
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(interruptEnReg), 
                    writeCallback: (_, value) => editArray(interruptEnReg, (int)value))
                ;

            // Channel (0~7) Interrupt Mode Register ~ Offset 0x54
            Registers.IntrMode0.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch7IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[7]; },
                    writeCallback: (_, value) => {channelInterruptMode[7] = (InterruptMode) (uint)value;})
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch6IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[6]; },
                    writeCallback: (_, value) => {channelInterruptMode[6] = (InterruptMode) (uint)value;})
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch5IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[5]; },
                    writeCallback: (_, value) => {channelInterruptMode[5] = (InterruptMode) (uint)value;})
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch4IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[4]; },
                    writeCallback: (_, value) => {channelInterruptMode[4] = (InterruptMode) (uint)value;})
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch3IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[3]; },
                    writeCallback: (_, value) => {channelInterruptMode[3] = (InterruptMode) (uint)value;})
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch2IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[2]; },
                    writeCallback: (_, value) => {channelInterruptMode[2] = (InterruptMode) (uint)value;})
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch1IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[1]; },
                    writeCallback: (_, value) => {channelInterruptMode[1] = (InterruptMode) (uint)value;})
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch0IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[0]; },
                    writeCallback: (_, value) => {channelInterruptMode[0] = (InterruptMode) (uint)value;})
                ;

            // Channel (8~15) Interrupt Mode Register ~ Offset 0x58
            Registers.IntrMode1.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch15IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[15]; },
                    writeCallback: (_, value) => {channelInterruptMode[15] = (InterruptMode) (uint)value;})
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch14IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[14]; },
                    writeCallback: (_, value) => {channelInterruptMode[14] = (InterruptMode) (uint)value;})
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch13IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[13]; },
                    writeCallback: (_, value) => {channelInterruptMode[13] = (InterruptMode) (uint)value;})
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch12IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[12]; },
                    writeCallback: (_, value) => {channelInterruptMode[12] = (InterruptMode) (uint)value;})
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch11IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[11]; },
                    writeCallback: (_, value) => {channelInterruptMode[11] = (InterruptMode) (uint)value;})
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch10IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[10]; },
                    writeCallback: (_, value) => {channelInterruptMode[10] = (InterruptMode) (uint)value;})
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch9IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[9]; },
                    writeCallback: (_, value) => {channelInterruptMode[9] = (InterruptMode) (uint)value;})
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch8IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[8]; },
                    writeCallback: (_, value) => {channelInterruptMode[8] = (InterruptMode) (uint)value;})
                ;

            // Channel (16~23) Interrupt Mode Register ~ Offset 0x5C
            Registers.IntrMode2.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch23IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[23]; },
                    writeCallback: (_, value) => {channelInterruptMode[23] = (InterruptMode) (uint)value;})
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch22IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[22]; },
                    writeCallback: (_, value) => {channelInterruptMode[22] = (InterruptMode) (uint)value;})
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch21IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[21]; },
                    writeCallback: (_, value) => {channelInterruptMode[21] = (InterruptMode) (uint)value;})
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch20IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[20]; },
                    writeCallback: (_, value) => {channelInterruptMode[20] = (InterruptMode) (uint)value;})
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch19IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[19]; },
                    writeCallback: (_, value) => {channelInterruptMode[19] = (InterruptMode) (uint)value;})
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch18IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[18]; },
                    writeCallback: (_, value) => {channelInterruptMode[18] = (InterruptMode) (uint)value;})
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch17IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[17]; },
                    writeCallback: (_, value) => {channelInterruptMode[17] = (InterruptMode) (uint)value;})
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch16IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[16]; },
                    writeCallback: (_, value) => {channelInterruptMode[16] = (InterruptMode) (uint)value;})
                ;

            // Channel (24~31) Interrupt Mode Register ~ Offset 0x60
            Registers.IntrMode3.Define(this)
                .WithReservedBits(31, 1)
                .WithValueField(28, 3, FieldMode.Read | FieldMode.Write, name: "Ch31IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[31]; },
                    writeCallback: (_, value) => {channelInterruptMode[31] = (InterruptMode) (uint)value;})
                .WithReservedBits(27, 1)
                .WithValueField(24, 3, FieldMode.Read | FieldMode.Write, name: "Ch30IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[30]; },
                    writeCallback: (_, value) => {channelInterruptMode[30] = (InterruptMode) (uint)value;})
                .WithReservedBits(23, 1)
                .WithValueField(20, 3, FieldMode.Read | FieldMode.Write, name: "Ch29IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[29]; },
                    writeCallback: (_, value) => {channelInterruptMode[29] = (InterruptMode) (uint)value;})
                .WithReservedBits(19, 1)
                .WithValueField(16, 3, FieldMode.Read | FieldMode.Write, name: "Ch28IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[28]; },
                    writeCallback: (_, value) => {channelInterruptMode[28] = (InterruptMode) (uint)value;})
                .WithReservedBits(15, 1)
                .WithValueField(12, 3, FieldMode.Read | FieldMode.Write, name: "Ch27IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[27]; },
                    writeCallback: (_, value) => {channelInterruptMode[27] = (InterruptMode) (uint)value;})
                .WithReservedBits(11, 1)
                .WithValueField(8, 3, FieldMode.Read | FieldMode.Write, name: "Ch26IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[26]; },
                    writeCallback: (_, value) => {channelInterruptMode[26] = (InterruptMode) (uint)value;})
                .WithReservedBits(7, 1)
                .WithValueField(4, 3, FieldMode.Read | FieldMode.Write, name: "Ch25IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[25]; },
                    writeCallback: (_, value) => {channelInterruptMode[25] = (InterruptMode) (uint)value;})
                .WithReservedBits(3, 1)
                .WithValueField(0, 3, FieldMode.Read | FieldMode.Write, name: "Ch24IntrM",
                    valueProviderCallback: _ => { return (uint)channelInterruptMode[24]; },
                    writeCallback: (_, value) => {channelInterruptMode[24] = (InterruptMode) (uint)value;})
                ;


            // Channel Interrupt Status Register ~ Offset 0x64
            Registers.IntrStatus.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.WriteOneToClear, name: "IntrStatus",
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(interruptStatusReg), 
                    writeCallback: (_, value) => {
                        Console.WriteLine("IntrStatus: value = {0}", value);
                        for (var i = 0; i < interruptStatusReg.Length; i++){
                            if (Convert.ToBoolean((value >> i) & 0x1) == true){
                                interruptStatusReg[i] = !Convert.ToBoolean((value >> i) & 0x1);
                            }                            
                        }
                    })

                ;

            // De-bounce Enable Register ~ Offset 0x70
            Registers.DeBounceEn.Define(this)
                .WithValueField(0, ATCGPIO100_GPIO_NUM, FieldMode.Read | FieldMode.Write, name: "DeBounceEn",
                    valueProviderCallback: _ => ReturnArrayAsDoubleWord(deBounceEnReg), 
                    writeCallback: (_, value) => editArray(deBounceEnReg, (int)value))
                ;

            // De-bounce Control Register ~ Offset 0x74
            Registers.DeBounceCtrl.Define(this)
                .WithFlag(31, FieldMode.Read | FieldMode.Write, name: "DBClkSel",
                    writeCallback: (_, value) => {DebounceClkSelValue = value;},
                    valueProviderCallback: _ => DebounceClkSelValue)
                .WithReservedBits(8, 23)
                .WithValueField(0, 8, FieldMode.Read | FieldMode.Write, name: "DBPreScale",
                    writeCallback: (_, value) => {DebouncePreScaleValue = (uint)value;},
                    valueProviderCallback: _ => DebouncePreScaleValue)
                ;

        }


        private void HandlePinStateChangeInterrupt(int pinIdx, bool risingEdge)
        {
            // var mode = interruptMode[pinIdx].Value;
            // if(mode == InterruptEnable.EnabledOnAnyTransition
            //     || (risingEdge && mode == InterruptEnable.EnabledOnRisingEdgeTransition)
            //     || (!risingEdge && mode == InterruptEnable.EnabledOnFallingEdgeTransition))
            // {
            //     this.Log(LogLevel.Noisy, "Triggering IRQ #{0} on the {1} edge", pinIdx, risingEdge ? "rising" : "falling");
            //     TriggerInterrupt(pinIdx);
            // }
        }

        private void SetOutputPinValue(int pinIdx, bool state)
        {

        }

        private void TriggerInterrupt(int pinIdx)
        {

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

        private bool[] dataInReg = new bool[ATCGPIO100_GPIO_NUM];
        private bool[] dataOutReg = new bool[ATCGPIO100_GPIO_NUM];
        private bool[] channelDirReg = new bool[ATCGPIO100_GPIO_NUM]; //0: input, 1: output
        private bool[] pullEnReg = new bool[ATCGPIO100_GPIO_NUM];
        private bool[] pullTypeReg = new bool[ATCGPIO100_GPIO_NUM]; //0: pull-up, 1: pull-down
        private bool[] interruptEnReg = new bool[ATCGPIO100_GPIO_NUM];
        private InterruptMode[] channelInterruptMode = new InterruptMode[ATCGPIO100_GPIO_NUM];
        private bool[] interruptStatusReg = new bool[ATCGPIO100_GPIO_NUM];
        private bool[] deBounceEnReg = new bool[ATCGPIO100_GPIO_NUM];
        private bool DebounceClkSelValue = new bool();
        private uint DebouncePreScaleValue = new uint();

        private const int ATCGPIO100_GPIO_NUM = 32; //number of gpio channels (pins)
        private const bool ATCGPIO100_PULL_SUPPORT = false; //Pull option IS NOT configured
        private const bool ATCGPIO100_INTR_SUPPORT = true; //interrupt option IS configured
        private const bool ATCGPIO100_DEBOUNCE_SUPPORT = false; //de-bounce option IS NOT configured

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
            //Reserved = 0x4,
            NegativeEdge = 0x5,
            PositiveEdge = 0x6,
            DualEdge = 0x7
        }
    }
}
