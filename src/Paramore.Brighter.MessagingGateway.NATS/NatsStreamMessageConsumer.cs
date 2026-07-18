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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.JetStream;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatsStreamMessageConsumer(IAsyncEnumerable<INatsJSMsg<byte[]>> messagesBuffer) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var obj)
            || obj is not INatsJSMsg<byte[]> natsMsg)
        {
            return;
        }

        await natsMsg.AckAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var obj)
            || obj is not INatsJSMsg<byte[]> natsMsg)
        {
            return false;
        }

        await natsMsg.AckAsync(cancellationToken: cancellationToken);
        return true;
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeOut.HasValue)
        {
            cts.CancelAfter(timeOut.Value);
        }

        try
        {
            await foreach (var message in messagesBuffer.WithCancellation(cts.Token))
            {
                return [message.ToMessage()];
            }

            return [new Message()];
        }
        catch (OperationCanceledException)
        {
            return [new Message()];
        }
    }

    public async Task NackAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var obj)
            || obj is not INatsJSMsg<byte[]> natsMsg)
        {
            return;
        }

        await natsMsg.NakAsync(cancellationToken: cancellationToken);
    }

    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue(HeadersName.NatsMessage, out var obj)
            || obj is not INatsJSMsg<byte[]> natsMsg)
        {
            return false;
        }

        await natsMsg.NakAsync(cancellationToken: cancellationToken);
        return true;
    }
    
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }

    public void Acknowledge(Message message)
    {
        BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));
    }

    public bool Reject(Message message, MessageRejectionReason? reason = null)
    {
        return BrighterAsyncContext.Run(async () => await RejectAsync(message, reason));
    }

    public void Purge()
    {
        BrighterAsyncContext.Run(async () => await PurgeAsync());
    }

    public Message[] Receive(TimeSpan? timeOut = null)
    {
        return BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));
    }

    public void Nack(Message message)
    {
        BrighterAsyncContext.Run(async () => await NackAsync(message));
    }

    public bool Requeue(Message message, TimeSpan? delay = null)
    {
        return BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));
    }
}
