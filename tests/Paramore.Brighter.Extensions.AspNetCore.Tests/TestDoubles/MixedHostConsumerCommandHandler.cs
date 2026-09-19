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

using System;
using System.Threading;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A sync handler for <see cref="MixedHostConsumerCommand"/>, consumed by a real message pump. Records
/// itself into an injected <see cref="MixedHostConsumerRecorder"/> on construction, and its own
/// disposal, then signals an injected <see cref="CountdownEvent"/> so a test's own thread can tell when
/// a batch of messages has finished processing without polling.
/// </summary>
public sealed class MixedHostConsumerCommandHandler : RequestHandler<MixedHostConsumerCommand>, IDisposable
{
    private readonly CountdownEvent _countdown;

    public MixedHostConsumerCommandHandler(MixedHostConsumerRecorder recorder, CountdownEvent countdown)
    {
        _countdown = countdown;
        recorder.RecordHandler(this);
    }

    /// <summary>
    /// Whether the container has disposed this instance.
    /// </summary>
    public bool IsDisposed { get; private set; }

    public override MixedHostConsumerCommand Handle(MixedHostConsumerCommand command)
    {
        var result = base.Handle(command);
        _countdown.Signal();
        return result;
    }

    public void Dispose() => IsDisposed = true;
}
