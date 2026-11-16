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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class AsyncBreakerBulkSweepTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, FakeTimeProvider clock, IAmAnOutboxCircuitBreaker breaker)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingBulkProducerAsync(_orders, fails: true)
            }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            timeProvider: clock,
            outboxCircuitBreaker: breaker);

    [Fact]
    public async Task When_a_bulk_sweep_runs_with_an_async_breaker_should_trip_through_the_batch_and_write_once()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new CountingTrippedTopicStore();
        var breaker = new CallCountingAsyncCircuitBreaker(new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock));
        var outbox = new InMemoryOutbox(clock);
        var mediator = NewMediator(outbox, clock, breaker);
        var context = new RequestContext();
        foreach (var _ in Enumerable.Range(0, 100))
            await mediator.AddToOutboxAsync(NewMessage(_orders), context);

        // Act
        await mediator.ClearOutstandingFromOutboxAsync(100, TimeSpan.Zero, true, context);

        // Assert
        Assert.Equal(0, breaker.DirectTrips);
        var write = Assert.Single(store.Writes);
        Assert.Equal([_orders], write.Select(trip => trip.Topic).ToArray());
        Assert.Equal(1, breaker.BatchesBegun);
    }
}
