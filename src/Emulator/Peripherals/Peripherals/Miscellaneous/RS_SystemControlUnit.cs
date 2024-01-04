using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class RS_SystemControlUnit<T> : BasicDoubleWordPeripheral, IKnownSize
        where T : BaseCPU, ICPUWithNMI
    {
        public RS_SystemControlUnit(IMachine machine,long version, T bcpu, T acpu) : base(machine)
        {
            this.version = version;
        }
        private void DefineRegisters()
        {
            long idrevReset = (version == (long)RS_SystemControlUnitVersion.Gemini) ? 0x10475253 : 0x10565253;
            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IdRev, new DoubleWordRegister(this, 0x10475253)},
            };
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
    }
}