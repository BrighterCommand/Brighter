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

public class MidCallDispatchAfterTripTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _invoices = new("invoices");
    private readonly List<Id> _attempted = new();
    private IReadOnlyList<RoutingKey> _listedWhenInvoicesSent = Array.Empty<RoutingKey>();
    private int _writesWhenInvoicesSent = -1;

    private Message NewMessage(RoutingKey topic)
        => new(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("{}"));

    private OutboxProducerMediator<Message, CommittableTransaction> NewMediator(
        InMemoryOutbox outbox, FakeTimeProvider clock, DistributedOutboxCircuitBreaker breaker, CountingTrippedTopicStore store)
        => new(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new ObservingProducerAsync(_orders, fails: true, message => _attempted.Add(message.Id)),
                [_invoices] = new ObservingProducerAsync(_invoices, fails: false, message =>
                {
                    _attempted.Add(message.Id);
                    _listedWhenInvoicesSent = breaker.TrippedTopics.ToArray();
                    _writesWhenInvoicesSent = store.Writes.Count;
                })
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
    public async Task When_the_first_message_trips_its_topic_should_still_attempt_later_messages_for_it()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: clock);
        var mediator = NewMediator(new InMemoryOutbox(clock), clock, breaker, store);
        var context = new RequestContext();
        var firstOrder = NewMessage(_orders);
        var secondOrder = NewMessage(_orders);
        var invoice = NewMessage(_invoices);
        Message[] batch = [firstOrder, secondOrder, invoice];
        foreach (var message in batch) await mediator.AddToOutboxAsync(message, context);

        // Act
        await mediator.ClearOutboxAsync(batch.Select(message => message.Id), context);

        // Assert
        Assert.Equal([firstOrder.Id, secondOrder.Id, invoice.Id], _attempted.Distinct().ToArray());
        Assert.Equal([_orders], _listedWhenInvoicesSent);
        Assert.Equal(0, _writesWhenInvoicesSent);
        var write = Assert.Single(store.Writes);
        Assert.Equal([_orders], write.Select(trip => trip.Topic).ToArray());
    }
}
