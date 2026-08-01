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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation;

public class AsyncPublishConfirmationTests
{
    private static readonly RoutingKey s_topic = new("Async.Publish.Confirmation.Topic");

    [Fact]
    public async Task When_disposing_async_confirmation_producer_should_await_callback()
    {
        // Arrange
        var requestContext = new RequestContext();
        var message = CreateMessage();
        var outbox = new GatedAsyncOutbox();
        await outbox.AddAsync(message, requestContext);
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_topic })
        {
            UseAsyncPublishConfirmation = true
        };
        ConfigureMediator(producer, outbox);

        // Act
        producer.Send(message);
        await outbox.DispatchStarted.WaitAsync(TimeSpan.FromSeconds(1));
        var disposeTask = producer.DisposeAsync().AsTask();

        try
        {
            // Assert
            await Assert.ThrowsAsync<TimeoutException>(
                async () => await disposeTask.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            outbox.AllowDispatch();
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(1));
        }

        Assert.True(outbox.WasDispatched(message.Id, requestContext));
    }

    [Fact]
    public async Task When_async_confirmation_is_off_should_not_wait_for_async_dispatch()
    {
        // Arrange
        var requestContext = new RequestContext();
        var message = CreateMessage();
        var outbox = new GatedAsyncOutbox();
        await outbox.AddAsync(message, requestContext);
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_topic });
        ConfigureMediator(producer, outbox);

        // Act
        var sendTask = Task.Run(() => producer.Send(message));
        await outbox.DispatchStarted.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            // Assert
            await sendTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            outbox.AllowDispatch();
            await sendTask.WaitAsync(TimeSpan.FromSeconds(1));
            await outbox.DispatchCompleted.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task When_disposing_with_concurrent_sends_should_drain_every_confirmation()
    {
        // Arrange
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_topic })
        {
            UseAsyncPublishConfirmation = true
        };
        var confirmed = new ConcurrentBag<string>();
        ((ISupportPublishConfirmationAsync)producer).OnMessagePublishedAsync += async result =>
        {
            await Task.Yield();
            confirmed.Add(result.MessageId.Value);
        };
        var messages = Enumerable.Range(0, 5).Select(_ => CreateMessage()).ToArray();

        // Act: concurrent sends race the pump start; dispose must still drain every callback.
        await Task.WhenAll(messages.Select(message => Task.Run(() => producer.Send(message))));
        await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(
            messages.Select(message => message.Id.Value).OrderBy(id => id),
            confirmed.OrderBy(id => id));
    }

    [Fact]
    public async Task When_an_async_confirmation_subscriber_throws_dispose_still_drains()
    {
        // Arrange
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = s_topic })
        {
            UseAsyncPublishConfirmation = true
        };
        var confirmed = new List<string>();
        ((ISupportPublishConfirmationAsync)producer).OnMessagePublishedAsync += result =>
        {
            confirmed.Add(result.MessageId.Value);
            throw new InvalidOperationException("subscriber fault");
        };
        var firstMessage = CreateMessage();
        var secondMessage = CreateMessage();

        // Act: the first callback's throw must not fault the drain worker, strand the second
        // message, or re-surface out of DisposeAsync.
        producer.Send(firstMessage);
        producer.Send(secondMessage);
        await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(new[] { firstMessage.Id.Value, secondMessage.Id.Value }, confirmed);
    }

    private static Message CreateMessage() => new(
        new MessageHeader(Id.Random(), s_topic, MessageType.MT_EVENT),
        new MessageBody("test"));

    private static void ConfigureMediator(InMemoryMessageProducer producer, IAmAnOutbox outbox)
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
