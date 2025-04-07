using System;
using System.Globalization;

namespace Paramore.Brighter.Extensions;

/// <summary>
/// The <see cref="DateTimeOffset"/> extensions
/// </summary>
public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Convert to <see cref="DateTimeOffset"/> to <seealso href="https://datatracker.ietf.org/doc/html/rfc3339">RFC 3339</seealso>
    /// </summary>
    /// <param name="datetime">The <see cref="DateTimeOffset"/> to be converted</param>
    /// <returns>A date time in <seealso href="https://datatracker.ietf.org/doc/html/rfc3339">RFC 3339</seealso> format.</returns>
    public static string ToRcf3339(this DateTimeOffset datetime) 
        => datetime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo);
}
