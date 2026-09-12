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

using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records the handler instance and <see cref="ISingletonDependency"/> instance
/// <see cref="PlaceSingletonOrderHandler"/> was constructed with, once per <c>Send</c>, so a test can
/// compare them across two separate HTTP requests. Register as a singleton in the container under test.
/// </summary>
public sealed class SingletonOrderRecorder
{
    private readonly List<(object Handler, ISingletonDependency Dependency)> _entries = new();

    /// <summary>
    /// The handler instance and dependency instance recorded on each <c>Send</c>, in order.
    /// </summary>
    public IReadOnlyList<(object Handler, ISingletonDependency Dependency)> Entries => _entries;

    /// <summary>
    /// Records one <c>Send</c>'s handler instance and the <see cref="ISingletonDependency"/> it was
    /// constructed with.
    /// </summary>
    public void Record(object handler, ISingletonDependency dependency) => _entries.Add((handler, dependency));
}
