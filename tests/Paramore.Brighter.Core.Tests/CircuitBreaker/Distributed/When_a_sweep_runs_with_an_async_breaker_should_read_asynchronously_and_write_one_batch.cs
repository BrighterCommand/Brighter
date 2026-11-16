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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class AsyncBreakerSweepTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _invoices = new("invoices");

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, FakeTimeProvider clock, IAmAnOutboxCircuitBreaker breaker, bool ordersFail)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingProducerAsync(_orders, fails: ordersFail),
                [_invoices] = new TopicFailingProducerAsync(_invoices, fails: false)
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
    public async Task When_a_sweep_runs_with_an_async_breaker_should_read_asynchronously_and_write_one_batch()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new SignallingTrippedTopicStore(new InMemoryTrippedTopicStore());
        var breakerA = new CallCountingAsyncCircuitBreaker(new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock));
        var breakerB = new CallCountingAsyncCircuitBreaker(new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock));
        var outboxA = new InMemoryOutbox(clock);
        var outboxB = new InMemoryOutbox(clock);
        var mediatorA = NewMediator(outboxA, clock, breakerA, ordersFail: true);
        var mediatorB = NewMediator(outboxB, clock, breakerB, ordersFail: false);
        var context = new RequestContext();
        await mediatorA.AddToOutboxAsync(NewMessage(_orders), context);
        var ordersAtB = NewMessage(_orders);
        var invoiceAtB = NewMessage(_invoices);
        await mediatorB.AddToOutboxAsync(ordersAtB, context);
        await mediatorB.AddToOutboxAsync(invoiceAtB, context);

        // Act
        await mediatorA.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);
        var tripsInStoreWhenAReturned = store.WrittenTrips.Select(trip => trip.Topic).ToArray();
        await mediatorB.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);

        // Assert
        Assert.Equal([_orders], tripsInStoreWhenAReturned);
        Assert.Equal([ordersAtB.Id], outboxB.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray());
        Assert.Equal([invoiceAtB.Id], outboxB.DispatchedMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray());
        Assert.Equal(1, breakerA.CoolDowns);
        Assert.Equal(1, breakerB.CoolDowns);
        Assert.Equal(1, breakerA.BatchesBegun);
        Assert.Equal(0, breakerA.DirectTrips);
        Assert.Equal(1, breakerA.AsyncReads);
        Assert.Equal(1, breakerB.AsyncReads);
        Assert.Equal(0, breakerA.SyncListings);
        Assert.Equal(0, breakerB.SyncListings);
    }
}
