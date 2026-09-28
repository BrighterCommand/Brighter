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
using System.Threading;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Records construction and disposal identity and order on a shared log, so a test can assert not
/// just that two DI resolutions are distinct but that the first was disposed before the second was
/// constructed. Register as a singleton in the container under test and inject into a mapper,
/// transform or dependency double's constructor and <c>Dispose</c>: call <see cref="RecordConstruction"/>
/// once per instance construction and <see cref="RecordDisposal"/> with the id it returned.
/// </summary>
public sealed class ConstructionOrderRecorder
{
    private readonly List<string> _events = new();
    private int _nextId;

    /// <summary>
    /// The events in construction/disposal order, e.g. <c>["Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2"]</c>.
    /// </summary>
    public IReadOnlyList<string> Events
    {
        get { lock (_events) return _events.ToArray(); }
    }

    /// <summary>
    /// Records a new construction and returns its id, to be passed to <see cref="RecordDisposal"/>.
    /// </summary>
    public int RecordConstruction()
    {
        var id = Interlocked.Increment(ref _nextId);
        lock (_events) _events.Add($"Constructed:{id}");
        return id;
    }

    /// <summary>
    /// Records the disposal of the instance identified by <paramref name="id"/>.
    /// </summary>
    public void RecordDisposal(int id)
    {
        lock (_events) _events.Add($"Disposed:{id}");
    }
}
