//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class ComparingTimer : ITimer, IPeripheral
    {
        public ComparingTimer(IClockSource clockSource, long frequency, IPeripheral owner, string localName, ulong limit = ulong.MaxValue, Direction direction = Direction.Ascending, bool enabled = false, WorkMode workMode = WorkMode.OneShot, bool eventEnabled = false, ulong compare = ulong.MaxValue, uint divider = 1, uint step = 1)
        {
            if(compare > limit)
            {
                throw new ConstructionException(string.Format(CompareHigherThanLimitMessage, compare, limit));
            }
            if(divider == 0)
            {
                throw new ArgumentException("Divider cannot be zero.");
            }
            if(frequency == 0)
            {
                throw new ArgumentException("Frequency cannot be zero.");
            }
            if(limit == 0)
            {
                throw new ArgumentException("Limit cannot be zero.");
            }

            this.clockSource = clockSource;

            initialDirection = direction;
            initialFrequency = frequency;
            initialLimit = limit;
            initialCompare = compare;
            initialEnabled = enabled;
            initialWorkMode = workMode;
            initialEventEnabled = eventEnabled;
            initialDivider = divider;
            initialStep = step;
            this.owner = this is IPeripheral && owner == null ? this : owner;
            this.localName = localName;
            InternalReset();
            this.Log(LogLevel.Info, "Creating ComparingTimers with freq: {0}, limit: 0x{1:X}, compare: {2}, clockSource: {3}, workMode: {4}", frequency, limit, compare, clockSource, workMode);
        }

        protected ComparingTimer(IClockSource clockSource, long frequency, ulong limit = ulong.MaxValue, Direction direction = Direction.Ascending, bool enabled = false, WorkMode workMode = WorkMode.OneShot, bool eventEnabled = false, ulong compare = ulong.MaxValue, uint divider = 1, uint step = 1) 
            : this(clockSource, frequency, null, null, limit, direction, enabled, workMode, eventEnabled, compare, divider, step)
        {
        }

        public bool Enabled
        {
            get
            {
                return clockSource.GetClockEntry(CompareReachedInternal).Enabled;
            }
            set
            {
                
                clockSource.ExchangeClockEntryWith(CompareReachedInternal, oldEntry => oldEntry.With(enabled: value));
                this.Log(LogLevel.Info, "Setting ComparingTimers enabled to: {0}", value);
            }
        }

        public bool EventEnabled { get; set; }

        public long Frequency
        {
            get
            {
                return frequency;
            }
            set
            {
                if(value == 0)
                {
                    throw new ArgumentException("Frequency cannot be zero.");
                }
                frequency = value;
                RecalculateFrequency();
            }
        }

        public ulong Value
        {
            get
            {
                var currentValue = 0UL;
                clockSource.GetClockEntryInLockContext(CompareReachedInternal, entry =>
                {
                    currentValue = valueAccumulatedSoFar + entry.Value;
                });
                this.Log(LogLevel.Info, "ComparingTimers value: {0}", currentValue);
                return currentValue;
            }
            set
            {
                if(value > initialLimit)
                {
                    throw new ArgumentException("Value cannot be larger than limit");
                }

                clockSource.ExchangeClockEntryWith(CompareReachedInternal, entry =>
                {
                    valueAccumulatedSoFar = value;
                    this.Log(LogLevel.Info, "ComparingTimers value: 0x{0:X}", value);
                    return entry.With(period: CalculatePeriod(), value: 0);
                });
            }
        }

        public ulong Compare
        {
            get
            {
                return compareValue;
            }
            set
            {
                if(value > initialLimit)
                {
                    throw new InvalidOperationException(CompareHigherThanLimitMessage.FormatWith(value, initialLimit));
                }
                clockSource.ExchangeClockEntryWith(CompareReachedInternal, entry =>
                {
                    compareValue = value;
                    valueAccumulatedSoFar += entry.Value;
                    this.Log(LogLevel.Info, "ComparingTimer compare value: 0x{0:X} , valueAccumulatedSoFar: 0x{1:X}", value, valueAccumulatedSoFar);
                    return entry.With(period: CalculatePeriod(), value: 0);
                });
            }
        }

        public uint Divider
        {
            get
            {
                return divider;
            }
            set
            {
                if(value == divider)
                {
                    return;
                }
                if(value == 0)
                {
                    throw new ArgumentException("Divider cannot be zero.");
                }
                divider = value;
                RecalculateFrequency();
            }
        }

        public uint Step
        {
            get
            {
                return step;
            }
            set
            {
                if(value == step)
                {
                    return;
                }
                step = value;
                clockSource.ExchangeClockEntryWith(CompareReachedInternal, oldEntry => oldEntry.With(step: step));
            }
        }

        public virtual void Reset()
        {
            InternalReset();
        }

        public event Action CompareReached;

        protected virtual void OnCompareReached()
        {
            if(!EventEnabled)
            {
                return;
            }
            this.Log(LogLevel.Info, "ComparingTimers: reaching CompareReached");

            CompareReached?.Invoke();
        }

        private void RecalculateFrequency()
        {
            var effectiveFrequency = Frequency / Divider;
            clockSource.ExchangeClockEntryWith(CompareReachedInternal, oldEntry => oldEntry.With(frequency: effectiveFrequency));
        }

        private ulong CalculatePeriod()
        {
            return ((compareValue > valueAccumulatedSoFar) ? compareValue : initialLimit) - valueAccumulatedSoFar;
        }

        private void CompareReachedInternal()
        {
            // since we use OneShot, timer's value is already 0 and it is disabled now
            // first we add old limit to accumulated value:
            this.Log(LogLevel.Info, "Reaching CompareReachedInternal");
            valueAccumulatedSoFar += clockSource.GetClockEntry(CompareReachedInternal).Period;
            if(valueAccumulatedSoFar >= initialLimit && compareValue != initialLimit)
            {
                // compare value wasn't actually reached, the timer reached its limit
                // we don't trigger an event in such case
                valueAccumulatedSoFar = 0;
                clockSource.ExchangeClockEntryWith(CompareReachedInternal, entry => entry.With(period: compareValue, enabled: true));
                return;
            }
            // real compare event - then we reenable the timer with the next event marked by limit
            // which will probably be soon corrected by software
            clockSource.ExchangeClockEntryWith(CompareReachedInternal, entry => entry.With(period: initialLimit - valueAccumulatedSoFar, enabled: true));
            if(valueAccumulatedSoFar >= initialLimit)
            {
                valueAccumulatedSoFar = 0;
            }
            OnCompareReached();
        }

        private void InternalReset()
        {
            divider = initialDivider;
            frequency = initialFrequency;
            step = initialStep;

            var clockEntry = new ClockEntry(initialCompare, frequency / divider, CompareReachedInternal, owner, localName, initialEnabled, initialDirection, initialWorkMode, step)
            { Value = initialDirection == Direction.Ascending ? 0 : initialLimit };
            clockSource.ExchangeClockEntryWith(CompareReachedInternal, entry => clockEntry, () => clockEntry);
            valueAccumulatedSoFar = 0;
            compareValue = initialCompare;
            EventEnabled = initialEventEnabled;
        }

        private ulong valueAccumulatedSoFar;
        private ulong compareValue;
        private uint divider;
        private long frequency;
        private uint step;

        private readonly uint initialStep;
        private readonly uint initialDivider;
        private readonly Direction initialDirection;
        private readonly long initialFrequency;
        private readonly IClockSource clockSource;
        private readonly ulong initialLimit;
        private readonly WorkMode initialWorkMode;
        private readonly ulong initialCompare;
        private readonly bool initialEnabled;
        private readonly bool initialEventEnabled;
        private readonly IPeripheral owner;
        private readonly string localName;

        private const string CompareHigherThanLimitMessage = "Compare value ({0}) cannot be higher than limit ({1}).";
    }
}

