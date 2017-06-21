using System;

namespace Paramore.Brighter.Time
{
    public static class Clock
    {
        private static DateTime? _overrideTimeUtc;

        public static DateTime? OverrideTime
        {
            get => _overrideTimeUtc;
            set
            {
                if (value != null) _overrideTimeUtc = value.Value.ToUniversalTime();
            }
        }

        public static DateTime Now()
        {
            if (OverrideTime.HasValue)
            {
                var overrideTime = OverrideTime.Value;
                OverrideTime = OverrideTime.Value.AddMilliseconds(50);
                return overrideTime;
            }

            
            return DateTime.UtcNow;
        }

        public static void Clear()
        {
            _overrideTimeUtc = null;
        }
    }
}