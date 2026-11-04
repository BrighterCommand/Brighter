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

public class CooldownPassedSweepTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, FakeTimeProvider clock, IAmAnOutboxCircuitBreaker breaker, bool ordersFail)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingProducerAsync(_orders, fails: ordersFail)
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
    public async Task When_the_cooldown_duration_has_passed_a_sweep_should_include_the_topic_again()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new InMemoryTrippedTopicStore();
        var breakerA = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var breakerB = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        var outboxB = new InMemoryOutbox(clock);
        var mediatorA = NewMediator(new InMemoryOutbox(clock), clock, breakerA, ordersFail: true);
        var mediatorB = NewMediator(outboxB, clock, breakerB, ordersFail: false);
        var context = new RequestContext();
        await mediatorA.AddToOutboxAsync(
            new Message(new MessageHeader(Id.Random(), _orders, MessageType.MT_COMMAND), new MessageBody("{}")), context);
        var ordersAtB = new Message(new MessageHeader(Id.Random(), _orders, MessageType.MT_COMMAND), new MessageBody("{}"));
        await mediatorB.AddToOutboxAsync(ordersAtB, context);

        // Act
        await mediatorA.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);
        await mediatorB.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);
        var outstandingBeforeCooldown = outboxB.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        clock.Advance(s_options.CooldownDuration + TimeSpan.FromSeconds(1));
        await mediatorB.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);
        var dispatchedAfterCooldown = outboxB.DispatchedMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        // Assert
        Assert.Equal([ordersAtB.Id], outstandingBeforeCooldown);
        Assert.Equal([ordersAtB.Id], dispatchedAfterCooldown);
    }
}
