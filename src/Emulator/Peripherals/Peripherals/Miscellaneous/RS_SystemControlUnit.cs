using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RS_SystemControlUnit<T> : BasicDoubleWordPeripheral, IKnownSize, IGPIOReceiver
        where T : BaseCPU, ICPUWithNMI
    {
        public RS_SystemControlUnit(IMachine machine,long version, T bcpu = null, T acpu= null) : base(machine)
        {
            this.version = version;
            this.bcpu = bcpu;
            this.acpu = acpu;
        }
        private void DefineRegisters()
        {
            long idrevReset = (version == (long)RS_SystemControlUnitVersion.Gemini) ? 0x10475253 : 0x10565253;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IdRev, new DoubleWordRegister(this, 0x10475253)},
            };
        }

        public void OnGPIO(int number, bool value)
        {
            switch((InputPort)number){
                case InputPort.AcpuWdt:

                    break;
                case InputPort.BcpuWdt:
                    break;
                default:
                    break;
            }
            

        }
        private enum Registers : long
        {
            IdRev = 0x0,
            SoftwareResetConttrol = 0x4,
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
        public long Size => 0x3FFF;
        private long version;
        private T bcpu;
        private T acpu;

        public enum InputPort: int{
            
            Bootstrap0 = 0x0,
            Bootstrap1 = 0x1,
            BcpuWdt = 0x2,
            AcpuWdt = 0x3,
            Irq1 = 0x4,
            Irq2 = 0x5,
            Irq3 = 0x6,
            Irq4 = 0x7,
            Irq5 = 0x8,
            Irq6 = 0x9,
            Irq7 = 0x10,
            Irq8 = 0x11,
            Irq9 = 0x12,
            Irq10 = 0x13,
            Irq11 = 0x14,
            Irq12 = 0x15,
            Irq13 = 0x16,
            Irq14 = 0x17,
            Irq15 = 0x18,
            Irq16 = 0x19,
            Irq17 = 0x20,
            Irq18 = 0x21,
            Irq19 = 0x22,
            Irq20 = 0x23,
            Irq21 = 0x24,
            Irq22 = 0x25,
            Irq23 = 0x26,
            Irq24 = 0x27,
            Irq25 = 0x28,
            Irq26 = 0x29,
            Irq27 = 0x30,
            Irq28 = 0x31,
            Irq29 = 0x32,
            Irq30 = 0x33,
            Irq31 = 0x34
        }

        // WDTs not covered in output ports as NMI is called direcly via the OnNMI function 
        public enum OutputPorts: long{
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