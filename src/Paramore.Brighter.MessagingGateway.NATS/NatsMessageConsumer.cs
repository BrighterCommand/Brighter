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
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatsMessageConsumer(INatsSub<byte[]> subscription, INatsClient client) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeOut.HasValue)
            {
                cts.CancelAfter(timeOut.Value);
            }

            var message = await subscription.Msgs.ReadAsync(cts.Token);
            return [message.ToMessage()];
        }
        catch (OperationCanceledException)
        {
            return [new Message()];
        }
    }

    public Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var n) || n is not NatsMsg<byte[]> natsMsg)
        {
            return false;
        }

        await client.PublishAsync(message.Header.Topic.Value,
            natsMsg.Data,
            natsMsg.Headers,
            natsMsg.ReplyTo, cancellationToken: cancellationToken);

        return true;
    }

    

    
    public void Acknowledge(Message message)
    {
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        return true;
    }

    public void Purge()
    {
    }

    public Message[] Receive(TimeSpan? timeOut = null)
    {
        return BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));
    }

    public void Nack(Message message)
    {
    }

    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        return BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));
    }
    
    public void Dispose()
    {
        var task = DisposeAsync();
        if (!task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await subscription.DisposeAsync();
    }
}
