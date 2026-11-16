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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class RequestOnlyInstanceCannotWriteTripTests
{
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(10);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");

    private static bool NamesTheFailure(LogEvent e)
        => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("InvalidOperationException");

    private static async Task<bool> BecomesTrueAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + s_patience;
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        return condition();
    }

    private static bool WarningLogged() => TestCorrelator.GetLogEventsFromCurrentContext().Any(NamesTheFailure);

    private Message NewMessage()
        => new(new MessageHeader(Id.Random(), _orders, MessageType.MT_COMMAND), new MessageBody("{}"));

    private static DistributedOutboxCircuitBreaker NewBreaker(ThrowingOnWriteTrippedTopicStore store, FakeTimeProvider clock)
        => new(store, s_options, timeProvider: clock);

    [Fact]
    public async Task When_a_send_fails_and_the_store_cannot_write_the_trip_should_log_a_warning_and_keep_the_message()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new ThrowingOnWriteTrippedTopicStore();
        var outbox = new InMemoryOutbox(clock);
        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { [_orders] = new TopicFailingProducerAsync(_orders, fails: true) }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            timeProvider: clock,
            outboxCircuitBreaker: NewBreaker(store, clock));
        var requestContext = new RequestContext();
        var message = NewMessage();
        await mediator.AddToOutboxAsync(message, requestContext);

        // Act
        var thrown = await Record.ExceptionAsync(() => mediator.ClearOutboxAsync([message.Id], requestContext));
        var warned = await BecomesTrueAsync(WarningLogged);

        // Assert
        Assert.Null(thrown);
        Assert.True(warned);
        Assert.Equal([message.Id], outbox.OutstandingMessages(TimeSpan.Zero, requestContext).Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task When_a_confirmation_callback_cannot_write_a_trip_should_log_a_warning_and_keep_the_message()
    {
        using var context = TestCorrelator.CreateContext();
        // Arrange
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var store = new ThrowingOnWriteTrippedTopicStore();
        var outbox = new InMemoryOutbox(clock);
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = _orders })
        {
            UseAsyncPublishConfirmation = true,
            PublishFailurePredicate = _ => true
        };
        _ = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { [_orders] = producer }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            timeProvider: clock,
            outboxCircuitBreaker: NewBreaker(store, clock));
        var requestContext = new RequestContext();
        var message = NewMessage();
        await outbox.AddAsync(message, requestContext);

        // Act
        var thrown = await Record.ExceptionAsync(async () =>
        {
            await producer.SendAsync(message);
            await producer.DisposeAsync();
        });
        var warned = await BecomesTrueAsync(WarningLogged);

        // Assert
        Assert.Null(thrown);
        Assert.True(warned);
        Assert.Equal([message.Id], outbox.OutstandingMessages(TimeSpan.Zero, requestContext).Select(m => m.Id).ToArray());
    }
}
