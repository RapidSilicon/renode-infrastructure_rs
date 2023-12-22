//
// Copyright (c) 2023 Rapid Silicon
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Binding;
using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class Andes_N22 : RiscV32
    {

        public Andes_N22(IMachine machine, IRiscVTimeProvider timeProvider = null, string cpuType = "rv32imac", uint hartId = 0, uint resetVectorAddress = 0x0)
            : base(timeProvider: timeProvider, cpuType: cpuType, machine: machine, hartId: hartId, allowUnalignedAccesses: true, nmiVectorLength: 1, nmiVectorAddress: resetVectorAddress)
        {
            this.resetVectorAddress = resetVectorAddress;
            MXSTATUS = RegisterValue.Create(0, 32);
            MMISC_CTL = RegisterValue.Create(0, 32);

            AndestarV5CSR[] notImplementedRWCSRs = {
                AndestarV5CSR.MTVT_RW, AndestarV5CSR.MDLMB_RW,
                AndestarV5CSR.MXSTATUS_RW, AndestarV5CSR.MPFT_CTL_RW,
                AndestarV5CSR.MHSP_CTL_RW, AndestarV5CSR.MCACHE_CTL_RW,
                AndestarV5CSR.MIRQ_ENTRY_RW, AndestarV5CSR.MINTSEL_JAL,
                AndestarV5CSR.UITB_RW, AndestarV5CSR.UCODE_RW};
            AndestarV5CSR[] notImplementedROCSRs = { AndestarV5CSR.MMSC_CFG_RO };
            foreach (AndestarV5CSR csr in notImplementedRWCSRs)
            {
                RegisterCSR((ulong)csr,
                    () => LogUnhandledCSRRead(csr.ToString()),
                    val => LogUnhandledCSRWrite(csr.ToString(), val));
            }
            foreach (AndestarV5CSR csr in notImplementedROCSRs)
            {
                RegisterCSR((ulong)csr,
                    () => LogUnhandledCSRRead(csr.ToString()),
                    val => LogWriteOnReadOnlyCSR(csr.ToString(), val));
            }
            RegisterCSR((ulong)AndestarV5CSR.PUSHMEPC_RW, () => 0u, GeneratePushCSRWrite(MEPC));
            RegisterCSR((ulong)AndestarV5CSR.PUSHMCAUSE_RW, () => 0u, GeneratePushCSRWrite(MCAUSE));
            RegisterCSR((ulong)AndestarV5CSR.PUSHMXSTATUS_RW, () => 0u, GeneratePushCSRWrite(MXSTATUS));
            RegisterCSR((ulong)AndestarV5CSR.MNVEC_RO, () => NMIVectorAddress.Value, val => LogWriteOnReadOnlyCSR("MNVEC_RO", val));
            RegisterCSR((ulong)AndestarV5CSR.MMISC_CTL_RW, () => MMISC_CTL, (w) =>
            {
                // For now, only support special logic for when NMI is modified
                MMISC_CTL = w;
                bool newNMI = BitHelper.IsBitSet(w, 9);
                if (newNMI)
                {
                    NMIVectorAddress = MTVEC; 
                }
                else
                {
                    NMIVectorAddress = resetVectorAddress;
                }
            });

        }
        public override void Reset(){
            base.Reset();
            PC = resetVectorAddress;
        }

        private Action<ulong> GeneratePushCSRWrite(RegisterValue register)
        {
            Action<ulong> result = (write_val) =>
            {
                ulong opcode = (ulong)Bus.ReadDoubleWord(PC.RawValue, context: this);
                var func3 = BitHelper.GetValue(opcode, 12, 3);
                if (func3 != 0b101)
                {
                    this.Log(LogLevel.Error, "Operation not implemented for CSR");
                    return;
                }
                var uimm = write_val;
                var addr = SP + (uimm << 2);
                Bus.WriteDoubleWord(addr, register, this);
            };
            return result;
        }

        private ulong LogUnhandledCSRRead(string name)
        {
            this.Log(LogLevel.Error, "Reading from an unsupported CSR {0}", name);
            return 0u;
        }

        private void LogUnhandledCSRWrite(string name, ulong value)
        {
            this.Log(LogLevel.Error, "Writing to an unsupported CSR {0} value: 0x{1:X}", name, value);
        }
        private void LogWriteOnReadOnlyCSR(string name, ulong value)
        {
            this.Log(LogLevel.Error, "Writing to RO CSR {0} value: 0x{1:X}", name, value);
        }

        public override void OnNMI(int number, bool value, ulong? mcause = null)
        {

            bool newNmi = BitHelper.IsBitSet(MMISC_CTL, 9);
            base.OnNMI(number, value, newNmi ? 0xFFFUL : 1);
        }

        private RegisterValue MXSTATUS;
        private RegisterValue MMISC_CTL;
        private readonly uint resetVectorAddress;

        private enum AndestarV5CSR
        {
            MTVT_RW = 0x307,
            MDLMB_RW = 0x7c1,
            MNVEC_RO = 0x7c3,
            MXSTATUS_RW = 0x7c4,
            MPFT_CTL_RW = 0x7c5,
            MHSP_CTL_RW = 0x7c6,
            MCACHE_CTL_RW = 0x7ca,
            MMISC_CTL_RW = 0x7d0,
            PUSHMXSTATUS_RW = 0x7eb, // Hook to CSRRWI
            MIRQ_ENTRY_RW = 0x7ec,
            MINTSEL_JAL = 0x7ed,
            PUSHMCAUSE_RW = 0x7ee, // Hook to CSRRWI
            PUSHMEPC_RW = 0x7ef, // Hook to CSRRWI
            UITB_RW = 0x800,
            UCODE_RW = 0x801,
            MMSC_CFG_RO = 0xFC2
        }
    }
}
