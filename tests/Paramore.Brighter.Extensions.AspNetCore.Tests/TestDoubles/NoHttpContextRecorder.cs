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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records the <see cref="IMarker"/> resolved by each <see cref="NoHttpContextHandler"/> invocation
/// (AC-19), so a test can compare the instances a hosted service's and a background thread's own <c>Send</c>
/// calls each resolved. Register as a singleton in the container under test.
/// </summary>
public sealed class NoHttpContextRecorder
{
    private readonly ConcurrentBag<IMarker> _markers = new();

    /// <summary>
    /// The <see cref="IMarker"/> resolved by every <c>Send</c> call recorded so far.
    /// </summary>
    public IReadOnlyCollection<IMarker> Markers => _markers.ToArray();

    /// <summary>
    /// Records the <see cref="IMarker"/> a single <c>Send</c> call's handler was constructed with.
    /// </summary>
    public void Record(IMarker marker) => _markers.Add(marker);
}
