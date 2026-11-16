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

public class ClearOutboxBatchTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly RoutingKey _orders = new("orders");

    private Message NewMessage()
        => new(new MessageHeader(Id.Random(), _orders, MessageType.MT_COMMAND), new MessageBody("{}"));

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
    public async Task When_clear_outbox_async_dispatches_many_failing_messages_should_make_one_store_write_before_returning()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: clock);
        var outbox = new InMemoryOutbox(clock);
        var mediator = NewMediator(outbox, clock, breaker);
        var context = new RequestContext();
        var ids = new List<Id>();
        for (var i = 0; i < 100; i++)
        {
            var message = NewMessage();
            ids.Add(message.Id);
            await mediator.AddToOutboxAsync(message, context);
        }

        // Act
        await mediator.ClearOutboxAsync(ids, context);
        var writesWhenCallReturned = store.Writes.ToArray();

        // Assert
        var write = Assert.Single(writesWhenCallReturned);
        Assert.Equal([_orders], write.Select(trip => trip.Topic).ToArray());
    }
}
