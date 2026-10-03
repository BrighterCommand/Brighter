#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class RequeueFailureMessageMapper<TRequest>
    : IAmAMessageMapper<TRequest>, IAmAMessageMapperAsync<TRequest> where TRequest : class, IRequest, new()
{
    public IRequestContext? Context { get; set; }

    public Message MapToMessage(TRequest request, Publication publication)
        => new(new MessageHeader(request.Id, publication.Topic ?? RoutingKey.Empty, request.RequestToMessageType()), new MessageBody("payload"));

    public TRequest MapToRequest(Message message) => new() { Id = message.Id };

    public Task<Message> MapToMessageAsync(TRequest request, Publication publication, CancellationToken cancellationToken = default)
        => Task.FromResult(MapToMessage(request, publication));

    public Task<TRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        => Task.FromResult(MapToRequest(message));
}
