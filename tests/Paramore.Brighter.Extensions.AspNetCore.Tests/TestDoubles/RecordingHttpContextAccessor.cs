#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Microsoft.AspNetCore.Http;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An <see cref="IHttpContextAccessor"/> that counts every read of <see cref="HttpContext"/>, so a test
/// can assert whether a caller consulted it at all - not just what it would have returned.
/// </summary>
public sealed class RecordingHttpContextAccessor : IHttpContextAccessor
{
    private readonly HttpContext? _context;

    /// <summary>
    /// Constructs an accessor that always answers <paramref name="context"/> when read.
    /// </summary>
    public RecordingHttpContextAccessor(HttpContext? context) => _context = context;

    /// <summary>
    /// The number of times <see cref="HttpContext"/> has been read.
    /// </summary>
    public int HttpContextReadCount { get; private set; }

    /// <inheritdoc />
    public HttpContext? HttpContext
    {
        get
        {
            HttpContextReadCount++;
            return _context;
        }
        set => throw new System.NotSupportedException();
    }
}
