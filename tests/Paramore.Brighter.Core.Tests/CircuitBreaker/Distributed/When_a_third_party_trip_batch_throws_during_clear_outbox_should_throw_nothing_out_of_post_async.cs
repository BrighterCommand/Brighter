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
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class ThirdPartyTripBatchDuringClearOutboxTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _unregistered = new("unregistered");

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    private static bool IsTheBreakersFailureWarning(LogEvent e)
        => e.Level == LogEventLevel.Warning
           && e.Exception?.Message == ThrowingBatchCircuitBreaker.FailureMessage;

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, FakeTimeProvider clock, IAmAnOutboxCircuitBreaker breaker)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingProducerAsync(_orders, fails: true)
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
    public async Task When_a_third_party_breaker_throws_on_begin_during_clear_outbox_should_warn_trip_directly_and_throw_nothing()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: true);
        var mediator = NewMediator(new InMemoryOutbox(clock), clock, breaker);
        var context = new RequestContext();
        var orders = NewMessage(_orders);
        await mediator.AddToOutboxAsync(orders, context);

        // Act
        var thrown = await Record.ExceptionAsync(() => mediator.ClearOutboxAsync([orders.Id], context));

        // Assert
        Assert.Null(thrown);
        Assert.Equal(1, breaker.DirectTrips);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), IsTheBreakersFailureWarning);
    }

    [Fact]
    public async Task When_a_third_party_batch_throws_on_write_during_clear_outbox_should_warn_and_throw_nothing()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: false);
        var mediator = NewMediator(new InMemoryOutbox(clock), clock, breaker);
        var context = new RequestContext();
        var orders = NewMessage(_orders);
        await mediator.AddToOutboxAsync(orders, context);

        // Act
        var thrown = await Record.ExceptionAsync(() => mediator.ClearOutboxAsync([orders.Id], context));

        // Assert
        Assert.Null(thrown);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), IsTheBreakersFailureWarning);
    }

    [Fact]
    public async Task When_a_third_party_batch_throws_on_write_after_a_dispatch_exception_during_clear_outbox_should_keep_the_dispatch_exception()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: false);
        var mediator = NewMediator(new InMemoryOutbox(clock), clock, breaker);
        var context = new RequestContext();
        var unregistered = NewMessage(_unregistered);
        await mediator.AddToOutboxAsync(unregistered, context);

        // Act
        var thrown = await Record.ExceptionAsync(() => mediator.ClearOutboxAsync([unregistered.Id], context));

        // Assert
        Assert.NotNull(thrown);
        Assert.NotEqual(ThrowingBatchCircuitBreaker.FailureMessage, thrown.Message);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), IsTheBreakersFailureWarning);
    }
}
