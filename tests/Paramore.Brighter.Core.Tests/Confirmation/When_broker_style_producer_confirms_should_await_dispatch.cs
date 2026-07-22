#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation;

public class BrokerStyleAsyncConfirmationTests
{
    private static readonly RoutingKey s_topic = new("Broker.Style.Confirmation.Topic");

    [Fact]
    public async Task When_broker_style_producer_confirms_should_await_dispatch()
    {
        // Arrange
        var requestContext = new RequestContext();
        var message = CreateMessage();
        var outbox = new GatedAsyncOutbox();
        await outbox.AddAsync(message, requestContext);
        var producer = new StubConfirmingProducerAsync(s_topic);
        ConfigureMediator(producer, outbox);

        // Act: raise the confirmation as a broker ack handler would; the producer awaits the
        // mediator's callback, so the raise must not complete until the outbox gate opens.
        var raiseTask = producer.RaiseConfirmationAsync(
            new PublishConfirmationResult(true, message.Id, s_topic, null));
        await outbox.DispatchStarted.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            // Assert
            await Assert.ThrowsAsync<TimeoutException>(
                async () => await raiseTask.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            outbox.AllowDispatch();
            await raiseTask.WaitAsync(TimeSpan.FromSeconds(1));
        }

        Assert.True(outbox.WasDispatched(message.Id, requestContext));
    }

    [Fact]
    public void When_producer_supports_async_confirmation_mediator_should_not_subscribe_sync_event()
    {
        // Arrange
        var producer = new StubConfirmingProducerAsync(s_topic);

        // Act
        ConfigureMediator(producer, new GatedAsyncOutbox());

        // Assert: the mediator must register exactly one callback — the awaited async event — or a
        // confirmation would be handled twice (once awaited, once fire-and-forget).
        Assert.False(producer.SyncCallbackSubscribed);
    }

    private static Message CreateMessage() => new(
        new MessageHeader(Id.Random(), s_topic, MessageType.MT_EVENT),
        new MessageBody("test"));

    private static void ConfigureMediator(StubConfirmingProducerAsync producer, IAmAnOutbox outbox)
    {
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { [s_topic] = producer });

        _ = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox,
            new InMemoryOutboxCircuitBreaker());
    }
}
