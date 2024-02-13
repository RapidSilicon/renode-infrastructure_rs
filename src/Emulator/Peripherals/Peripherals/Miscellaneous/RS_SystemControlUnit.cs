using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Peripherals.Timers;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RS_SystemControlUnit : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver, INumberedGPIOOutput
    {
        public RS_SystemControlUnit(
            IMachine machine,
            long version,
            ICPUWithNMI bcpu = null,
            ICPUWithNMI acpu = null,
            TranslationCPU acpuCtrl = null
            ) : base(machine)
        {
            this.version = (RS_SystemControlUnitVersion)version;
            this.bcpu = bcpu;
            this.acpu = acpu;
            this.acpuCtrl = acpuCtrl;

            GptPause = new GPIO();
            BcpuWdtPause = new GPIO();
            AcpuWdtPause = new GPIO();
            //ResetSystem = new GPIO();
            ResetBus = new GPIO();
            ResetSram = new GPIO();
            //ResetAcpu = new GPIO();
            ResetPeripheral = new GPIO();
            ResetFpga0 = new GPIO();
            ResetFpga1 = new GPIO();
            ResetDdr = new GPIO();
            ResetUsb = new GPIO();
            ResetEmac = new GPIO();
            ResetDma = new GPIO();

            var irqCount = 31;
            bcpuIrqSetBaseIndex = 0;
            fpgaIrqSetBaseIndex = bcpuIrqSetBaseIndex + irqCount;
            acpuIrqSetBaseIndex = fpgaIrqSetBaseIndex + irqCount;
            var connectionCount = acpuIrqSetBaseIndex + irqCount + 1;
            var connections = new Dictionary<int, IGPIO>();
            for (var i = 0; i < connectionCount; i++)
            {
                connections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(connections);
            irqMaskControl = new bool[31];
            irqMapControl = new IrqSubsystemMapping[31];
            irqState = new bool[31];
            DefineRegisters();
        }
        private void DefineRegisters()
        {
            uint idrevReset = (version == RS_SystemControlUnitVersion.Gemini) ? 0x10475253U : 0x10565253U;
            Registers.IdRev.Define(this, resetValue: idrevReset, name: "ID and Revision Register")
                .WithValueField(0, 16, mode: FieldMode.Read, name: "vendor_id")
                .WithValueField(16, 8, mode: FieldMode.Read, name: "chip_id")
                .WithValueField(24, 8, mode: FieldMode.Read, name: "rev_id");
            var swRstCtrlReg = Registers.SoftwareResetControl.Define(this, 0x0)
                .WithFlag(0, changeCallback: (oldVal, newVal) =>
                //ResetSystem.Set(newVal), name: "system_rstn")
                    { if (newVal) { machine.RequestReset();} }, name: "system_rstn")
                .WithFlag(1, changeCallback: (oldVal, newVal) =>
                    ResetBus.Set(newVal), name: "bus_rstn")
                .WithFlag(4, changeCallback: (oldVal, newVal) =>
                    ResetPeripheral.Set(newVal), name: "per_rstn")
                .WithFlag(5, changeCallback: (oldVal, newVal) =>
                    ResetFpga0.Set(newVal), name: "fpga0_rstn")
                .WithFlag(10, changeCallback: (oldVal, newVal) =>
                    ResetDma.Set(newVal), name: "dma_rstn")
                .WithReservedBits(11, 21); // P1
            Registers.PllConfig0.Define(this, 0x0); // P3  
            Registers.PllConfig1.Define(this, 0x0); // P3 
            Registers.PllConfig2.Define(this, 0x0); // P3 
            Registers.PllConfig3.Define(this, 0x0); // P3 
            Registers.PllConfig4.Define(this, 0x0); // P3 
            Registers.PllStatus.Define(this, 0x0);  // P3
            Registers.DividerControl.Define(this, 0x0); // P3
            Registers.GatingControl.Define(this, 0x0); // P3
            Registers.DebugControl.Define(this, 0x0); // P2, P3
            var wdtMaskReg = Registers.IrqAcpuBcpuWdtMask.Define(this, 0x0)
                .WithFlag(0, changeCallback: (oldVal, newVal) =>
                    {
                        bcpuNMImask = newVal;
                        refreshNMI();
                    },
                    name: "irq_mask_bcpu_wdt")
                .WithReservedBits(1, 15)
                .WithReservedBits(17, 15);
            Registers.IrqMaskMapControl_n.DefineMany(this, 30, setup: (reg, index) =>
                {
                    // Define value field and flags
                    reg.DefineFlagField(0, name: $"IRQ {index + 1} mask", changeCallback: (oldVal, newVal) =>
                    {
                        irqMaskControl[index] = newVal;
                        refreshExternalInterrupt(index);
                    });
                    reg.Reserved(1, 15);
                    reg.DefineEnumField<IrqSubsystemMapping>(16, 3, name: $"IRQ {index + 1} map", changeCallback: (oldVal, newVal) =>
                    {
                        irqMapControl[index] = newVal;
                        refreshExternalInterrupt(index);
                    });
                    reg.Reserved(19, 13);
                },
                resetValue: 0x0U, name: "IrqMaskMapControl_n");

            Registers.IsolationControl.Define(this, 0x0); // P3
            Registers.BootstrapStatus.Define(this, 0x0)
                .WithEnumField<DoubleWordRegister, ClockSelection>(0, 2, mode: FieldMode.Read,
                    valueProviderCallback: (_) =>
                    {
                        return (ClockSelection)clockSelection;
                    }, name: "clk_sel_status"
                )
                .WithEnumField<DoubleWordRegister, Bootmode>(2, 2, mode: FieldMode.Read,
                    valueProviderCallback: (_) =>
                    {
                        return (Bootmode)bootmode;
                    }, name: "bootm")
                .WithReservedBits(4, 1)
                .WithFlag(5, mode: FieldMode.Read, valueProviderCallback: (_) =>
                {
                    return (resetCause == BcpuResetCause.SystemReset);
                }, name: "bcpu_sw_rst_status")
                .WithFlag(6, mode: FieldMode.Read, valueProviderCallback: (_) =>
                {
                    return (resetCause == BcpuResetCause.PllLock);
                }, name: "bcpu_lock_rst_status")
                .WithFlag(7, mode: FieldMode.Read, valueProviderCallback: (_) =>
                {
                    return (resetCause == BcpuResetCause.Wdt);
                }, name: "bcpu_wdt_rst_status")
                ;
            Registers.MainDividerControl.Define(this, 0x0);
            Registers.PufccControl.Define(this, 0x0);
            Registers.FpgaPll.Define(this, 0x0); // P3
            var wdtPauseReg = Registers.WdtPause.Define(this, 0x0)
                .WithFlag(0, changeCallback: (oldVal, newVal) =>
                    BcpuWdtPause.Set(newVal),
                    name: "bcpu_wdt_pause")
                .WithReservedBits(1, 7)
                .WithReservedBits(9, 23);
            Registers.OscillatorControl.Define(this, 0x0); // dummy
            Registers.GptPause.Define(this, 0x0)
                .WithFlag(0, changeCallback: (oldVal, newVal) =>
                    GptPause.Set(newVal),
                    name: "pit_pause")
                .WithReservedBits(1, 31);
            Registers.ConfigSubSystemMemoryMargin.Define(this, 0x0); // dummy
            Registers.PadsModeControlSlewRateControl.Define(this, 0x0); // dummy
            Registers.SpareReg.Define(this, 0x0)
                .WithValueField(0, 32, name: "spare_reg");

            if (version == RS_SystemControlUnitVersion.Gemini)
            {
                swRstCtrlReg
                    .WithFlag(2, changeCallback: (oldVal, newVal) =>
                        ResetSram.Set(newVal), name: "sram_rstn")
                    .WithFlag(3, changeCallback: (oldVal, newVal) =>
                    //ResetAcpu.Set(newVal), name: "acpu_rstn")
                        { if (newVal) { acpuCtrl.Reset();} }, name: "acpu_rstn")
                    .WithFlag(6, changeCallback: (oldVal, newVal) =>
                        ResetFpga1.Set(newVal), name: "fpga1_rstn")
                    .WithFlag(7, changeCallback: (oldVal, newVal) =>
                        ResetDdr.Set(newVal), name: "ddr_rstn")
                    .WithFlag(8, changeCallback: (oldVal, newVal) =>
                        ResetUsb.Set(newVal), name: "usb_rstn")
                    .WithFlag(9, changeCallback: (oldVal, newVal) =>
                        ResetEmac.Set(newVal), name: "emac_rstn");
                wdtMaskReg
                    .WithFlag(16, changeCallback: (oldVal, newVal) =>
                        {
                            acpuNMImask = newVal;
                            refreshNMI();
                        }, name: "irq_mask_acpu_wdt");
                wdtPauseReg.WithFlag(8, changeCallback: (oldVal, newVal) =>
                    AcpuWdtPause.Set(newVal),
                    name: "acpu_wdt_pause");
                Registers.UsbControl.Define(this, 0x0);
                Registers.DDRMode.Define(this, 0x0); // dummy
                Registers.SramControl0.Define(this, 0x0); // dummy
                Registers.SramControl1.Define(this, 0x0); // dummy
                Registers.AcpuResetVector.Define(this, 0x80020000); // dummy
                Registers.AcpuMemoryMargin.Define(this, 0x0); // dummy
                Registers.MemorySubSystemMemoryMargin.Define(this, 0x0); // dummy
            }
            else
            {
                swRstCtrlReg.WithReservedBits(2, 1); // sram
                swRstCtrlReg.WithReservedBits(3, 1); // acpu
                swRstCtrlReg.WithReservedBits(6, 1); // fpga0
                swRstCtrlReg.WithReservedBits(7, 1); // ddr
                swRstCtrlReg.WithReservedBits(8, 1); // usb
                swRstCtrlReg.WithReservedBits(9, 1); // emac
                wdtMaskReg.WithReservedBits(16, 1); // acpu mask
                wdtPauseReg.WithReservedBits(8, 1); // acpu pause
            }

        }
        public override void Reset()
        {
            foreach (var pair in Connections)
            {
                pair.Value.Set(false);
            }
            base.Reset();

        }

        public void OnGPIO(int number, bool value)
        {
            switch (number)
            {
                case (int)InputPort.ClkSel0:
                    BitHelper.SetBit(ref clockSelection, 0, value);
                    break;
                case (int)InputPort.ClkSel1:
                    BitHelper.SetBit(ref clockSelection, 1, value);
                    break;
                case (int)InputPort.Bootstrap0:
                    BitHelper.SetBit(ref bootmode, 0, value);
                    break;
                case (int)InputPort.Bootstrap1:
                    BitHelper.SetBit(ref bootmode, 1, value);
                    break;
                case (int)InputPort.BcpuWdt:
                    bcpuNMIState = value;
                    refreshNMI();
                    break;
                case (int)InputPort.AcpuWdt:
                    acpuNMIState = value;
                    refreshNMI();
                    break;
                case int n when ((n >= (int)InputPort.Irq1) && (n <= (int)InputPort.Irq31)):
                    var irqIndex = n - (int)InputPort.Irq1;
                    irqState[irqIndex] = value;
                    refreshExternalInterrupt(irqIndex);
                    break;
                default:
                    break;
            }
        }
        private void refreshExternalInterrupt(int irqIndex)
        {
            var bcpuIrqGPIOIndex = bcpuIrqSetBaseIndex + irqIndex;
            var fpgaIrqGPIOIndex = fpgaIrqSetBaseIndex + irqIndex;
            var acpuIrqGPIOIndex = acpuIrqSetBaseIndex + irqIndex;
            //var maskMapCtrl = irqMaskMapControl[irqIndex];
            //uint map= BitHelper.GetValue(maskMapCtrl, 16, 3);
            //bool masked = BitHelper.IsBitSet(maskMapCtrl, 0);
            bool state = irqState[irqIndex];
            bool bcpuIrq = false;
            bool fpgaIrq = false;
            bool acpuIrq = false;
            if (!irqMaskControl[irqIndex])
            {
                switch (irqIndex)
                {
                    case 10: // acpu Mailbox
                        acpuIrq = state;
                        break;
                    case 11: // bcpu Mailbox
                        bcpuIrq = state;
                        break;
                    case 12: // fpga0 Mailbox
                        fpgaIrq = state;
                        break;
                    case 13: // fpga1 Mailbox
                        fpgaIrq = state;
                        break;
                    default:
                        switch (irqMapControl[irqIndex])
                        {
                            case IrqSubsystemMapping.Bcpu:
                                bcpuIrq = state;
                                break;
                            case IrqSubsystemMapping.Fpga:
                                fpgaIrq = state;
                                break;
                            case IrqSubsystemMapping.Acpu:
                                acpuIrq = state;
                                break;
                            default:
                                this.Log(LogLevel.Warning, $"Invalid IRQ mapping detected for external Irq {irqIndex}");
                                break;
                        }
                        break;
                }
            }
            Connections[bcpuIrqGPIOIndex].Set(bcpuIrq);
            Connections[fpgaIrqGPIOIndex].Set(fpgaIrq);
            if (version == RS_SystemControlUnitVersion.Gemini) { Connections[acpuIrqGPIOIndex].Set(acpuIrq); }
        }
        private void refreshNMI()
        {
            bool bcpuNMIVal = bcpuNMImask ? false : bcpuNMIState;
            bcpu.OnNMI(0, bcpuNMIVal, null);

            if (version == RS_SystemControlUnitVersion.Gemini)
            {
                bool acpuNMIVal = acpuNMImask ? false : bcpuNMIState;
                acpu.OnNMI(0, acpuNMIVal, null);
            }
        }
        public long Size => 0x3FFF;
        private RS_SystemControlUnitVersion version;
        private ICPUWithNMI bcpu;
        private ICPUWithNMI acpu;
        private TranslationCPU acpuCtrl;
        private uint clockSelection = 0x0;
        private uint bootmode = 0x0;
        private BcpuResetCause resetCause = BcpuResetCause.SystemReset;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; }
        private bool bcpuNMImask;
        private bool bcpuNMIState;
        private bool acpuNMImask;
        private bool acpuNMIState;
        private bool[] irqState;
        private int bcpuIrqSetBaseIndex;
        private int fpgaIrqSetBaseIndex;
        private int acpuIrqSetBaseIndex;
        private bool[] irqMaskControl;
        private IrqSubsystemMapping[] irqMapControl;

        public GPIO GptPause { get; }
        public GPIO BcpuWdtPause { get; }
        public GPIO AcpuWdtPause { get; }
        //public GPIO ResetSystem { get; } // Reset the whole machine
        public GPIO ResetBus { get; }
        public GPIO ResetSram { get; }
        public GPIO ResetAcpu { get; }
        public GPIO ResetPeripheral { get; }
        public GPIO ResetFpga0 { get; }
        public GPIO ResetFpga1 { get; }
        public GPIO ResetDdr { get; }
        public GPIO ResetUsb { get; }
        public GPIO ResetEmac { get; }
        public GPIO ResetDma { get; }
        private enum Registers
        {
            IdRev = 0x0,
            SoftwareResetControl = 0x4,
            PllConfig0 = 0x8,
            PllConfig1 = 0xC,
            PllConfig2 = 0x10,
            PllConfig3 = 0x14,
            PllConfig4 = 0x18,
            PllStatus = 0x1C,
            DividerControl = 0x20,
            GatingControl = 0x24,
            DebugControl = 0x28,
            IrqAcpuBcpuWdtMask = 0x2C,
            IrqMaskMapControl_n = 0x30, // n in [1,31]
            IsolationControl = 0xAC,
            BootstrapStatus = 0xB0,
            MainDividerControl = 0xB4,
            PufccControl = 0xB8,
            UsbControl = 0xBC,
            FpgaPll = 0xC0,
            WdtPause = 0xC4,
            DDRMode = 0xC8,
            OscillatorControl = 0xCC,
            GptPause = 0xD0,
            SramControl0 = 0xD4,
            SramControl1 = 0xD8,
            AcpuResetVector = 0xDC,
            AcpuMemoryMargin = 0xE0,
            MemorySubSystemMemoryMargin = 0xE4,
            ConfigSubSystemMemoryMargin = 0xE8,
            PadsModeControlSlewRateControl = 0xEC,
            SpareReg = 0xF0
        }
        public enum RS_SystemControlUnitVersion : long
        {
            Gemini = 0x0,
            Virgo = 0x1
        }
        public enum ClockSelection : uint
        {
            PllOutputXtalRef = 0x0,
            PllOutputRcOscRef = 0x1,
            Xtal = 0x2,
            RcOsc = 0x3
        }
        public enum Bootmode : uint
        {
            JTAG = 0x0,
            SPI = 0x1,
            CLI = 0x2,
            Default = 0x3
        }
        public enum IrqSubsystemMapping : uint
        {
            Bcpu = 0x1,
            Acpu = 0x2,
            Fpga = 0x4,
        }
        public enum BcpuResetCause
        {
            SystemReset,
            PllLock,
            Wdt
        }
        public enum InputPort : int
        {
            ClkSel0 = 0x0,
            ClkSel1 = 0x1,
            Bootstrap0 = 0x2,
            Bootstrap1 = 0x3,
            BcpuWdt = 0x4,
            AcpuWdt = 0x5,
            Irq1 = 0x6,
            Irq2 = 0x7,
            Irq3 = 0x8,
            Irq4 = 0x9,
            Irq5 = 0x10,
            Irq6 = 0x11,
            Irq7 = 0x12,
            Irq8 = 0x13,
            Irq9 = 0x14,
            Irq10 = 0x15,
            Irq11 = 0x16,
            Irq12 = 0x17,
            Irq13 = 0x18,
            Irq14 = 0x19,
            Irq15 = 0x20,
            Irq16 = 0x21,
            Irq17 = 0x22,
            Irq18 = 0x23,
            Irq19 = 0x24,
            Irq20 = 0x25,
            Irq21 = 0x26,
            Irq22 = 0x27,
            Irq23 = 0x28,
            Irq24 = 0x29,
            Irq25 = 0x30,
            Irq26 = 0x31,
            Irq27 = 0x32,
            Irq28 = 0x33,
            Irq29 = 0x34,
            Irq30 = 0x35,
            Irq31 = 0x36
        }

        // WDTs not covered in output ports as NMI is called direcly via the OnNMI function 
        public enum OutputPorts : uint
        {
            BcpuIrq1,
            BcpuIrq2,
            BcpuIrq3,
            BcpuIrq4,
            BcpuIrq5,
            BcpuIrq6,
            BcpuIrq7,
            BcpuIrq8,
            BcpuIrq9,
            BcpuIrq10,
            BcpuIrq11,
            BcpuIrq12,
            BcpuIrq13,
            BcpuIrq14,
            BcpuIrq15,
            BcpuIrq16,
            BcpuIrq17,
            BcpuIrq18,
            BcpuIrq19,
            BcpuIrq20,
            BcpuIrq21,
            BcpuIrq22,
            BcpuIrq23,
            BcpuIrq24,
            BcpuIrq25,
            BcpuIrq26,
            BcpuIrq27,
            BcpuIrq28,
            BcpuIrq29,
            BcpuIrq30,
            BcpuIrq31,
            FpgaIrq1,
            FpgaIrq2,
            FpgaIrq3,
            FpgaIrq4,
            FpgaIrq5,
            FpgaIrq6,
            FpgaIrq7,
            FpgaIrq8,
            FpgaIrq9,
            FpgaIrq10,
            FpgaIrq11,
            FpgaIrq12,
            FpgaIrq13,
            FpgaIrq14,
            FpgaIrq15,
            FpgaIrq16,
            FpgaIrq17,
            FpgaIrq18,
            FpgaIrq19,
            FpgaIrq20,
            FpgaIrq21,
            FpgaIrq22,
            FpgaIrq23,
            FpgaIrq24,
            FpgaIrq25,
            FpgaIrq26,
            FpgaIrq27,
            FpgaIrq28,
            FpgaIrq29,
            FpgaIrq30,
            FpgaIrq31,
            AcpuIrq1,
            AcpuIrq2,
            AcpuIrq3,
            AcpuIrq4,
            AcpuIrq5,
            AcpuIrq6,
            AcpuIrq7,
            AcpuIrq8,
            AcpuIrq9,
            AcpuIrq10,
            AcpuIrq11,
            AcpuIrq12,
            AcpuIrq13,
            AcpuIrq14,
            AcpuIrq15,
            AcpuIrq16,
            AcpuIrq17,
            AcpuIrq18,
            AcpuIrq19,
            AcpuIrq20,
            AcpuIrq21,
            AcpuIrq22,
            AcpuIrq23,
            AcpuIrq24,
            AcpuIrq25,
            AcpuIrq26,
            AcpuIrq27,
            AcpuIrq28,
            AcpuIrq29,
            AcpuIrq30,
            AcpuIrq31
        }
    }
}
