using System;

namespace Paramore.Brighter.Time
{
    public static class Clock
    {
        public static DateTime? OverrideTime { get; set; }

        public static DateTime Now()
        {
            if (OverrideTime.HasValue)
            {
                var overrideTime = OverrideTime;
                OverrideTime = OverrideTime.Value.AddMilliseconds(50);
                return overrideTime.Value.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        public static void Clear()
        {
            OverrideTime = null;
        }
    }
}