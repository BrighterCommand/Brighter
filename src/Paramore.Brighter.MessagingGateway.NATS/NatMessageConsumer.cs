// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using NATS.Client.Core;
using NATS.Net;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatMessageConsumer(
    INatsSub<byte[]> subscription,
    INatsClient client) : IAmAMessageConsumerAsync
{
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.CompletedTask;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.FromResult(true);
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.CompletedTask;
    }

    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            var message = await subscription.Msgs.ReadAsync(cancellationToken);
            message.
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return Task.FromResult(false);
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    private Message ToMessage(NatsMsg<byte[]> natsMsg)
    {
        var message = new Message { Header = new MessageHeader
        {
            MessageId =  GetMessageId(natsMsg.Headers),
            Baggage = GetBaggage(natsMsg.Headers),
        }};
        
        message.Header.Topic = new RoutingKey(natsMsg.Subject);
        message.Body = new MessageBody(natsMsg.Data ?? []);
        if (!string.IsNullOrEmpty(natsMsg.ReplyTo))
        {
            message.Header.ReplyTo = new RoutingKey(natsMsg.ReplyTo!);
        }

        return message;
    }

    private static Id GetMessageId(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.Id, out var messageId) &&  messageId.Count == 1)
        {
            return Id.Create(messageId.ToString());
        }

        return Id.Random();
    }

    public static Baggage GetBaggage(NatsHeaders? headers)
    {
        if (headers != null && headers.TryGetValue(HeadersName.Baggage, out var baggage) && baggage.Count == 1)
        {
            return Baggage.FromString(baggage.ToString());
        }

        return new Baggage();
    }
}
