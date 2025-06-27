using System;

namespace Paramore.Brighter.AWS.V4.Tests.Helpers
{
    public static class StringHelper
    {
        public static string Truncate(this string str, int maxLength)
        {
            str = str.Trim();
            int right = Math.Min(str.Length, maxLength);
            return str.Substring(0, right);
        }
    }
}
