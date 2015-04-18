using System;

namespace paramore.brighter.commandprocessor
{
    public class Clock
    {
        public static DateTime? OverrideTime { get; set; }

        public static DateTime? Now()
        {
            if (OverrideTime.HasValue)
            {
                return OverrideTime;
            }
            else
            {
                return DateTime.UtcNow;
            }
        }

        public static void Clear()
        {
            OverrideTime = null;
        }
    }
}
