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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A mapper for <see cref="PostedCommand"/> that records its own construction and disposal identity
/// and order via an injected <see cref="ConstructionOrderRecorder"/>, so a test can assert that
/// successive <c>Post</c> calls resolve distinct <c>Scoped</c> mapper instances and that the first is
/// disposed before the second is constructed.
/// </summary>
public sealed class PostedMapper : IAmAMessageMapper<PostedCommand>, IDisposable
{
    private readonly ConstructionOrderRecorder _recorder;
    private readonly int _id;

    public PostedMapper(ConstructionOrderRecorder recorder)
    {
        _recorder = recorder;
        _id = recorder.RecordConstruction();
    }

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(PostedCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

    public PostedCommand MapToRequest(Message message) => new();

    public void Dispose() => _recorder.RecordDisposal(_id);
}
