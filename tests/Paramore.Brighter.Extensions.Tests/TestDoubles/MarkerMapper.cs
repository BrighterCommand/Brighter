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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A mapper for <see cref="MarkerCommand"/> that records the <see cref="IMarker"/> it was
/// constructed with via an injected <see cref="MarkerLog"/>, and whose <c>MapToRequest</c> runs a
/// <see cref="MarkerTransform"/> via <see cref="MarkerUnwrapWith"/>, so a test can assert the mapper
/// and its transform shared one <see cref="IMarker"/> instance for the pipeline.
/// </summary>
public sealed class MarkerMapper : IAmAMessageMapper<MarkerCommand>
{
    public MarkerMapper(IMarker marker, MarkerLog log)
    {
        Marker = marker;
        log.RecordMapper(marker);
    }

    /// <summary>
    /// The <see cref="IMarker"/> this mapper was constructed with.
    /// </summary>
    public IMarker Marker { get; }

    public IRequestContext? Context { get; set; }

    public Message MapToMessage(MarkerCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test"));

    [MarkerUnwrapWith(0)]
    public MarkerCommand MapToRequest(Message message) => new();
}
