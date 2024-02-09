//
// Copyright (c) 2010-2020 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ResetGenerator : IGPIOReceiver
    {
        public ResetGenerator(IPeripheral peripheral)
        {
            this.peripheral = peripheral;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                throw new ArgumentOutOfRangeException();
            }
            if(value){
                peripheral.Reset();
            }
        }

        public void Reset()
        {
            
        }
        private readonly IPeripheral peripheral;

    }
}

