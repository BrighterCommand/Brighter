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

using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An <see cref="IAmAScope"/> handle double that records its own disposal onto a shared
/// <see cref="ReleaseOrderRecorder"/>, so a test can assert a <see cref="HandlerLifetimeScope"/> disposes
/// the handle after releasing every tracked handler.
/// </summary>
public sealed class RecordingPipelineScope(ReleaseOrderRecorder recorder) : IAmAScope
{
    /// <summary>True once <see cref="Dispose"/> or <see cref="DisposeAsync"/> has run.</summary>
    public bool WasDisposed { get; private set; }

    public void Dispose()
    {
        WasDisposed = true;
        recorder.RecordScopeDisposed();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
