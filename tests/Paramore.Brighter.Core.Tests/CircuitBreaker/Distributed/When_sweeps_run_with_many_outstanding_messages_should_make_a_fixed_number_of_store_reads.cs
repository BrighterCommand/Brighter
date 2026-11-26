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
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class FixedStoreReadTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };
    private readonly RoutingKey _orders = new("orders");

    private async Task<(CountingTrippedTopicStore Store, OutboxProducerMediator<Message, CommittableTransaction> Mediator)> NewMediatorWithOutstanding(int outstanding)
    {
        var clock = new FakeTimeProvider(s_start);
        var store = new CountingTrippedTopicStore();
        var outbox = new InMemoryOutbox(clock);
        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [_orders] = new TopicFailingProducerAsync(_orders, fails: false)
            }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            timeProvider: clock,
            outboxCircuitBreaker: new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock));
        var context = new RequestContext();
        for (var i = 0; i < outstanding; i++)
            await mediator.AddToOutboxAsync(NewMessage(), context);
        return (store, mediator);
    }

    private Message NewMessage()
        => new(new MessageHeader(Id.Random(), _orders, MessageType.MT_COMMAND), new MessageBody("{}"));

    private static async Task<(int Reads, int Writes)> SweepAsync(int outstanding, FixedStoreReadTests fixture)
    {
        var (store, mediator) = await fixture.NewMediatorWithOutstanding(outstanding);
        await mediator.ClearOutstandingFromOutboxAsync(outstanding, TimeSpan.Zero, false, new RequestContext());
        return (store.Reads, store.Writes.Count);
    }

    private static async Task<int> WaitForReadsAsync(CountingTrippedTopicStore store)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (store.Reads == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        await Task.Delay(100);
        return store.Reads;
    }

    [Fact]
    public async Task When_sweeps_run_with_many_outstanding_messages_should_make_a_fixed_number_of_store_reads()
    {
        // Arrange
        // Act
        var (readsForTen, writesForTen) = await SweepAsync(10, this);
        var (readsForTenThousand, writesForTenThousand) = await SweepAsync(10_000, this);

        // Assert
        Assert.Equal(readsForTen, readsForTenThousand);
        Assert.Equal(1, readsForTen);
        Assert.Equal(0, writesForTen);
        Assert.Equal(0, writesForTenThousand);
    }

    [Fact]
    public async Task When_a_refresh_starts_should_make_a_fixed_number_of_store_reads()
    {
        // Arrange
        var clock = new FakeTimeProvider(s_start);
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);

        // Act
        breaker.StartRefresh();
        var reads = await WaitForReadsAsync(store);

        // Assert
        Assert.Equal(1, reads);
        Assert.Empty(store.Writes);
    }
}
