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

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics.Outbox;

public class When_adding_a_message_to_the_outbox_async_should_count_one_added
{
    private readonly RoutingKey _topic = new("order.created");
    private readonly SpyOutboxMeter _meter = new();
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;
    private readonly CountingOutbox _outbox;
    private readonly FakeTimeProvider _timeProvider = new();

    public When_adding_a_message_to_the_outbox_async_should_count_one_added()
    {
        var producer = new NonConfirmingProducerAsync(_topic);
        _outbox = new CountingOutbox(_timeProvider);

        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _topic, producer } }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox: _outbox,
            timeProvider: _timeProvider,
            outboxMeter: _meter);
    }

    [Fact]
    public async Task When_adding_a_message_to_the_outbox_async_should_count_one_added_()
    {
        var ctx = new RequestContext();
        var msg1 = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()),
            new MessageBody("{}"));
        var msg2 = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND,
                timeStamp: _timeProvider.GetUtcNow()),
            new MessageBody("{}"));

        await _mediator.AddToOutboxAsync(msg1, ctx);

        Assert.Single(_meter.Added);
        Assert.Equal(_topic, _meter.Added[0]);
        Assert.Empty(_meter.Cleared);
        Assert.Empty(_meter.Durations);

        await _mediator.AddToOutboxAsync(msg2, ctx);

        Assert.Equal(2, _meter.Added.Count);
        Assert.Empty(_meter.Cleared);
        Assert.Empty(_meter.Durations);
    }
}
