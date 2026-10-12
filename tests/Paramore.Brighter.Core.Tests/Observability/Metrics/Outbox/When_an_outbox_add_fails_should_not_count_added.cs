#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

// ACCEPTANCE — RED-first exemption: RecordAdded is placed after the !written throw.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics.Outbox;

public class When_an_outbox_add_fails_should_not_count_added
{
    private readonly RoutingKey _topic = new("order.created");
    private readonly SpyOutboxMeter _meter = new();
    private readonly FakeTimeProvider _timeProvider = new();

    private OutboxProducerMediator<Message, CommittableTransaction> BuildMediator(CountingOutbox outbox)
    {
        var producerAsync = new NonConfirmingProducerAsync(_topic);
        var producerSync = new NonConfirmingProducerSync(_topic);
        return new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _topic, producerAsync }
            }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            timeProvider: _timeProvider,
            outboxMeter: _meter);
    }

    private OutboxProducerMediator<Message, CommittableTransaction> BuildSyncMediator(CountingOutbox outbox)
    {
        var producer = new NonConfirmingProducerSync(_topic);
        return new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _topic, producer }
            }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            timeProvider: _timeProvider,
            outboxMeter: _meter);
    }

    [Fact]
    public async Task When_async_single_add_fails_should_not_count()
    {
        var outbox = new CountingOutbox(_timeProvider) { FailAdd = true };
        using var mediator = BuildMediator(outbox);
        var ctx = new RequestContext();
        var msg = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()), new MessageBody("{}"));

        await Assert.ThrowsAsync<ChannelFailureException>(
            () => mediator.AddToOutboxAsync(msg, ctx));
        Assert.Empty(_meter.Added);
    }

    [Fact]
    public async Task When_async_batch_add_fails_should_not_count()
    {
        var outbox = new CountingOutbox(_timeProvider) { FailAdd = true };
        using var mediator = BuildMediator(outbox);
        var ctx = new RequestContext();
        var batchId = mediator.StartBatchAddToOutbox();
        for (var i = 0; i < 3; i++)
        {
            var msg = new Message(
                new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                    timeStamp: _timeProvider.GetUtcNow()), new MessageBody("{}"));
            await mediator.AddToOutboxAsync(msg, ctx, batchId: batchId);
        }

        await Assert.ThrowsAsync<ChannelFailureException>(
            () => mediator.EndBatchAddToOutboxAsync(batchId, null, ctx, CancellationToken.None));
        Assert.Empty(_meter.Added);
    }

    [Fact]
    public void When_sync_single_add_fails_should_not_count()
    {
        var outbox = new CountingOutbox(_timeProvider) { FailAdd = true };
        using var mediator = BuildSyncMediator(outbox);
        var ctx = new RequestContext();
        var msg = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()), new MessageBody("{}"));

        Assert.Throws<ChannelFailureException>(() => mediator.AddToOutbox(msg, ctx));
        Assert.Empty(_meter.Added);
    }

    [Fact]
    public void When_sync_batch_add_fails_should_not_count()
    {
        var outbox = new CountingOutbox(_timeProvider) { FailAdd = true };
        using var mediator = BuildSyncMediator(outbox);
        var ctx = new RequestContext();
        var batchId = mediator.StartBatchAddToOutbox();
        for (var i = 0; i < 3; i++)
        {
            var msg = new Message(
                new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                    timeStamp: _timeProvider.GetUtcNow()), new MessageBody("{}"));
            mediator.AddToOutbox(msg, ctx, batchId: batchId);
        }

        Assert.Throws<ChannelFailureException>(() => mediator.EndBatchAddToOutbox(batchId, null, ctx));
        Assert.Empty(_meter.Added);
    }
}
