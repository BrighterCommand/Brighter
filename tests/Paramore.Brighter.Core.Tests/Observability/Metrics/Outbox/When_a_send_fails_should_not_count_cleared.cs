#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics.Outbox;

public class When_a_send_fails_should_not_count_cleared
{
    private readonly FakeTimeProvider _timeProvider = new();

    [Fact]
    public async Task Async_send_throws_cleared_stays_empty()
    {
        var topic = new RoutingKey("order.async-fail");
        var meter = new SpyOutboxMeter();
        var producer = new NonConfirmingProducerAsync(topic) { ThrowOnSend = true };
        var outbox = new CountingOutbox(_timeProvider);

        using var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { topic, producer } }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            timeProvider: _timeProvider,
            outboxMeter: meter);

        var ctx = new RequestContext();
        var msg = new Message(
            new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()),
            new MessageBody("{}"));

        await mediator.AddToOutboxAsync(msg, ctx);
        Assert.Single(meter.Added);

        await mediator.ClearOutboxAsync([msg.Id], ctx);

        Assert.Empty(meter.Cleared);
        Assert.Empty(meter.Durations);
    }

    [Fact]
    public void Sync_send_throws_cleared_stays_empty()
    {
        var topic = new RoutingKey("order.sync-fail");
        var meter = new SpyOutboxMeter();
        var producer = new ThrowingSyncProducer(topic);
        var outbox = new CountingOutbox(_timeProvider);

        using var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { topic, producer } }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            timeProvider: _timeProvider,
            outboxMeter: meter);

        var ctx = new RequestContext();
        var msg = new Message(
            new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()),
            new MessageBody("{}"));

        mediator.AddToOutbox(msg, ctx);
        Assert.Single(meter.Added);

        mediator.ClearOutbox([msg.Id], ctx);

        Assert.Empty(meter.Cleared);
        Assert.Empty(meter.Durations);
    }

    private sealed class ThrowingSyncProducer : IAmAMessageProducerSync
    {
        public ThrowingSyncProducer(RoutingKey topic)
        {
            Publication = new Publication { Topic = topic };
        }

        public Publication Publication { get; }
        public Activity? Span { get; set; }
        public IAmAMessageScheduler? Scheduler { get; set; }

        public void Send(Message message) =>
            throw new InvalidOperationException("ThrowingSyncProducer forced throw");

        public void SendWithDelay(Message message, TimeSpan? delay) =>
            throw new InvalidOperationException("ThrowingSyncProducer forced throw");

        public void Dispose() { }
    }
}
