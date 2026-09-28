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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Records, in resolution order, the <see cref="CountingDisposable"/> a handler was constructed with.
/// Register as a singleton in the container under test and inject into the handler under test, so a test
/// can assert its <see cref="CountingDisposable.DisposeCount"/> after the handler pipeline (and its owned
/// scope) has already been torn down, without holding a reference to the handler instance itself.
/// </summary>
public sealed class CountingDisposableRecorder
{
    private readonly List<CountingDisposable> _instances = new();

    /// <summary>
    /// The <see cref="CountingDisposable"/> each handler construction recorded, in order.
    /// </summary>
    public IReadOnlyList<CountingDisposable> Instances
    {
        get { lock (_instances) return _instances.ToArray(); }
    }

    /// <summary>
    /// Records the <see cref="CountingDisposable"/> a handler was constructed with.
    /// </summary>
    public void Record(CountingDisposable instance)
    {
        lock (_instances) _instances.Add(instance);
    }
}
