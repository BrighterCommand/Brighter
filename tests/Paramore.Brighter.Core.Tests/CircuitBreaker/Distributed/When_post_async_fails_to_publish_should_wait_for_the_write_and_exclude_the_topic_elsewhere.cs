#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class RequestPathTripWriteTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, TimeProvider clock, IAmAnOutboxCircuitBreaker breaker, bool ordersFail)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingProducerAsync(_orders, fails: ordersFail),
                [_payments] = new TopicFailingProducerAsync(_payments, fails: ordersFail)
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
    public async Task When_post_async_on_a_request_only_instance_fails_to_publish_should_wait_for_the_store_write_and_another_instances_sweep_should_exclude_the_topic()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var inner = new InMemoryTrippedTopicStore();
        var store = new AsynchronouslyWritingTrippedTopicStore(inner);
        var options = new OutboxCircuitBreakerOptions();
        var mediatorA = NewMediator(new InMemoryOutbox(clock), clock, new DistributedOutboxCircuitBreaker(store, options, timeProvider: clock), ordersFail: true);
        var outboxB = new InMemoryOutbox(clock);
        var mediatorB = NewMediator(outboxB, clock, new DistributedOutboxCircuitBreaker(store, options, timeProvider: clock), ordersFail: false);
        var context = new RequestContext();
        var postedAtA = NewMessage(_orders);
        await mediatorA.AddToOutboxAsync(postedAtA, context);
        var ordersAtB = NewMessage(_orders);
        await mediatorB.AddToOutboxAsync(ordersAtB, context);

        // Act
        await mediatorA.ClearOutboxAsync([postedAtA.Id], context);
        var tripsInStoreWhenAReturned = (await inner.ReadLiveAsync(new TripWindow(clock.GetUtcNow(), options.CooldownDuration))).Select(trip => trip.Topic).ToArray();
        await mediatorB.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);

        // Assert
        Assert.Equal([_orders], tripsInStoreWhenAReturned);
        Assert.Equal([ordersAtB.Id], outboxB.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task When_clear_outbox_async_runs_over_two_topics_with_a_store_that_never_completes_should_return_within_five_seconds()
    {
        // Arrange
        var clock = TimeProvider.System;
        var breaker = new DistributedOutboxCircuitBreaker(new NeverCompletingTrippedTopicStore(), new OutboxCircuitBreakerOptions(), timeProvider: clock);
        var mediator = NewMediator(new InMemoryOutbox(clock), clock, breaker, ordersFail: true);
        var context = new RequestContext();
        var orders = NewMessage(_orders);
        var payments = NewMessage(_payments);
        await mediator.AddToOutboxAsync(orders, context);
        await mediator.AddToOutboxAsync(payments, context);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await mediator.ClearOutboxAsync([orders.Id, payments.Id], context);

        // Assert
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
