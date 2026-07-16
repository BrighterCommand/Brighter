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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.JetStream;
using Paramore.Brighter.MessagingGateway.NATS.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.NATS;

public class NatsStreamMessageProducer(
    INatsJSContext jsContext,
    NatsStreamPublication publication,
    InstrumentationOptions instrumentations) : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    public Publication Publication => publication;
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }

    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.Nats, message, instrumentations);
        await jsContext.PublishAsync(publication.Topic!.Value, message.Body.ToByteArray(),
            headers: message.Header.ToNatsHeaders(),
            cancellationToken: cancellationToken);
    }

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }


    public void Send(Message message)
    {
        BrighterAsyncContext.Run(async () => await SendAsync(message));
    }

    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }
}
