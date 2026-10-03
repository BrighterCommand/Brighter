#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Maps claim-checked payloads through both posting pipelines.
/// </summary>
public sealed class OptionalTracingClaimCheckMapper :
    IAmAMessageMapper<OptionalTracingClaimCheckEvent>,
    IAmAMessageMapperAsync<OptionalTracingClaimCheckEvent>
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    [ClaimCheck(step: 0, thresholdInKb: 0)]
    public Message MapToMessage(OptionalTracingClaimCheckEvent request, Publication publication) =>
        new(new MessageHeader(request.Id, publication.Topic!, MessageType.MT_EVENT), new MessageBody(request.Text));

    /// <inheritdoc />
    public OptionalTracingClaimCheckEvent MapToRequest(Message message) => new() { Text = message.Body.Value };

    /// <inheritdoc />
    [ClaimCheck(step: 0, thresholdInKb: 0)]
    public Task<Message> MapToMessageAsync(OptionalTracingClaimCheckEvent request, Publication publication,
        CancellationToken cancellationToken = default) => Task.FromResult(MapToMessage(request, publication));

    /// <inheritdoc />
    public Task<OptionalTracingClaimCheckEvent> MapToRequestAsync(Message message,
        CancellationToken cancellationToken = default) => Task.FromResult(MapToRequest(message));
}
