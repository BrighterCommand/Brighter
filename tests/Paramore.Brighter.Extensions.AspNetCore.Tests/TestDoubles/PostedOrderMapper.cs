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
/// A <c>Scoped</c> mapper for <see cref="PostedOrderCommand"/> that records its own construction into
/// an injected <see cref="PostedOrderMapperRecorder"/> and counts how many times the container has
/// disposed it (AC-17), so a test can assert two <c>Post</c> calls in one request share one instance,
/// disposed only once, by ASP.NET, at end of request.
/// </summary>
public sealed class PostedOrderMapper : IAmAMessageMapper<PostedOrderCommand>, IDisposable
{
    private int _disposeCount;

    public PostedOrderMapper(PostedOrderMapperRecorder recorder)
    {
        recorder.RecordConstruction(this);
    }

    /// <summary>
    /// How many times the container has disposed this instance.
    /// </summary>
    public int DisposeCount => _disposeCount;

    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Message MapToMessage(PostedOrderCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, new RoutingKey("posted-order"), MessageType.MT_COMMAND), new MessageBody("test"));

    /// <inheritdoc />
    public PostedOrderCommand MapToRequest(Message message) => new();

    /// <inheritdoc />
    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}
