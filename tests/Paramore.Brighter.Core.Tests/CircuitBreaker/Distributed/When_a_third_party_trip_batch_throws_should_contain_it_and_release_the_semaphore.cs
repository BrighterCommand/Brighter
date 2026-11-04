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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class ThirdPartyTripBatchContainmentTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _invoices = new("invoices");
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
                [_orders] = new TopicFailingProducerAsync(_orders, fails: true),
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
    public async Task When_a_third_party_breaker_throws_on_begin_should_trip_directly_warn_and_carry_on()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: true);
        var outbox = new InMemoryOutbox(clock);
        var mediator = NewMediator(outbox, clock, breaker);
        var context = new RequestContext();
        var orders = NewMessage(_orders);
        var invoice = NewMessage(_invoices);
        await mediator.AddToOutboxAsync(orders, context);
        await mediator.AddToOutboxAsync(invoice, context);

        // Act
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);

        // Assert
        Assert.Equal(1, breaker.DirectTrips);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), IsTheBreakersFailureWarning);
        Assert.Equal([invoice.Id], outbox.DispatchedMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task When_a_third_party_batch_throws_on_write_should_warn_and_release_the_semaphore()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: false);
        var outbox = new InMemoryOutbox(clock);
        var mediator = NewMediator(outbox, clock, breaker);
        var context = new RequestContext();
        await mediator.AddToOutboxAsync(NewMessage(_orders), context);
        var invoice = NewMessage(_invoices);
        await mediator.AddToOutboxAsync(invoice, context);

        // Act
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);
        var warnedOnFirstSweep = TestCorrelator.GetLogEventsFromCurrentContext().Any(IsTheBreakersFailureWarning);
        var laterInvoice = NewMessage(_invoices);
        await mediator.AddToOutboxAsync(laterInvoice, context);
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);

        // Assert
        Assert.True(warnedOnFirstSweep);
        Assert.Equal(
            new[] { invoice.Id, laterInvoice.Id }.OrderBy(id => id.Value),
            outbox.DispatchedMessages(TimeSpan.Zero, context).Select(m => m.Id).OrderBy(id => id.Value));
    }

    [Fact]
    public async Task When_a_third_party_batch_throws_on_write_after_a_dispatch_failure_should_keep_the_dispatch_exception()
    {
        using var logs = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var breaker = new ThrowingBatchCircuitBreaker(throwOnBegin: false);
        var outbox = new InMemoryOutbox(clock);
        var mediator = NewMediator(outbox, clock, breaker);
        var context = new RequestContext();
        await mediator.AddToOutboxAsync(NewMessage(_unregistered), context);

        // Act
        var thrown = await Record.ExceptionAsync(
            () => mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context));
        var invoice = NewMessage(_invoices);
        await mediator.AddToOutboxAsync(invoice, context);
        outbox.Delete([outbox.OutstandingMessages(TimeSpan.Zero, context).First(m => m.Header.Topic == _unregistered).Id], context);
        await mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, context);

        // Assert
        Assert.NotNull(thrown);
        Assert.NotEqual(ThrowingBatchCircuitBreaker.FailureMessage, thrown.Message);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), IsTheBreakersFailureWarning);
        Assert.Equal([invoice.Id], outbox.DispatchedMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray());
    }
}
