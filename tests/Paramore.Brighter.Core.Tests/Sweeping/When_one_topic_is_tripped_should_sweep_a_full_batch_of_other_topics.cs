#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TrippedTopicDoesNotHoldBackOthersTests
{
    private const int BatchSize = 100;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_one_topic_is_tripped_should_sweep_a_full_batch_of_other_topics(bool distributed)
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var invoices = new RoutingKey("invoices");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(TimeProvider.System);
        var breaker = NewBreaker(distributed);
        var mediator = NewMediator(bus, outbox, breaker, [orders, invoices]);
        AddUndispatched(outbox, orders, 200);
        AddUndispatched(outbox, invoices, 100);
        breaker.TripTopic(orders);

        // Act
        await mediator.ClearOutstandingFromOutboxAsync(BatchSize, TimeSpan.Zero, false, new RequestContext());

        // Assert
        Assert.Empty(bus.Stream(orders));
        Assert.Equal(100, bus.Stream(invoices).Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_fifty_topics_are_tripped_should_dispatch_the_messages_of_a_fifty_first_topic(bool distributed)
    {
        // Arrange
        var tripped = Enumerable.Range(1, 50).Select(i => new RoutingKey($"tripped-{i}")).ToArray();
        var other = new RoutingKey("other");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(TimeProvider.System);
        var breaker = NewBreaker(distributed);
        var mediator = NewMediator(bus, outbox, breaker, tripped.Append(other));
        foreach (var topic in tripped) AddUndispatched(outbox, topic, 2);
        AddUndispatched(outbox, other, 3);
        foreach (var topic in tripped) breaker.TripTopic(topic);

        // Act
        await mediator.ClearOutstandingFromOutboxAsync(BatchSize, TimeSpan.Zero, false, new RequestContext());

        // Assert
        Assert.Equal(3, bus.Stream(other).Count());
        Assert.All(tripped, topic => Assert.Empty(bus.Stream(topic)));
    }

    private static IAmAnOutboxCircuitBreaker NewBreaker(bool distributed)
        => distributed
            ? new DistributedOutboxCircuitBreaker(new InMemoryTrippedTopicStore(), new OutboxCircuitBreakerOptions())
            : new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions());

    private static OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InternalBus bus, InMemoryOutbox outbox, IAmAnOutboxCircuitBreaker breaker, IEnumerable<RoutingKey> topics)
        => new(
            new ProducerRegistry(topics.ToDictionary(
                topic => topic,
                topic => (IAmAMessageProducer)new InMemoryMessageProducer(bus, new Publication { Topic = topic }))),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            new BrighterTracer(TimeProvider.System),
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            outboxCircuitBreaker: breaker);

    private static void AddUndispatched(InMemoryOutbox outbox, RoutingKey topic, int count)
    {
        for (var i = 0; i < count; i++)
            outbox.Add(
                new Message(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("awaiting dispatch")),
                new RequestContext());
    }
}
