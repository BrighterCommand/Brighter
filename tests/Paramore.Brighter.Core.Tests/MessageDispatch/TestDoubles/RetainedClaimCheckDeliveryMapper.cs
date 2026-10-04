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

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class RetainedClaimCheckDeliveryMapper(ClaimCheckPumpScenario scenario) :
    IAmAMessageMapper<RetainedClaimCheckDeliveryCommand>, IAmAMessageMapperAsync<RetainedClaimCheckDeliveryCommand>
{
    public IRequestContext? Context { get; set; }

    [RetrieveClaim(0, retain: true)]
    public RetainedClaimCheckDeliveryCommand MapToRequest(Message message)
    {
        scenario.MappedMessage = message;
        if (scenario.MappingFailure)
            throw new InvalidOperationException("Cannot map retrieved luggage");

        return new RetainedClaimCheckDeliveryCommand { Id = message.Id, Payload = message.Body.Value };
    }

    [RetrieveClaim(0, retain: true)]
    public async Task<RetainedClaimCheckDeliveryCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return MapToRequest(message);
    }

    public Message MapToMessage(RetainedClaimCheckDeliveryCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, publication.Topic ?? RoutingKey.Empty, MessageType.MT_COMMAND), new MessageBody(request.Payload));

    public Task<Message> MapToMessageAsync(RetainedClaimCheckDeliveryCommand request, Publication publication, CancellationToken cancellationToken = default) =>
        Task.FromResult(MapToMessage(request, publication));
}
