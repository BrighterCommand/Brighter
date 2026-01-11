#region Licence

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Globalization;

namespace Paramore.Brighter.Extensions;

public static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Convert to <see cref="DateTimeOffset"/> to <seealso href="https://datatracker.ietf.org/doc/html/rfc3339">RFC 3339</seealso>
    /// </summary>
    /// <param name="datetime">The <see cref="DateTimeOffset"/> to be converted</param>
    /// <returns>A date time in <seealso href="https://datatracker.ietf.org/doc/html/rfc3339">RFC 3339</seealso> format.</returns>
    public static string ToRfc3339(this DateTimeOffset datetime) 
        => datetime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", DateTimeFormatInfo.InvariantInfo);
}
