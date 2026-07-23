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
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation;

public class ConfirmationSpanEndIsolationTests
{
    [Fact]
    public async Task When_ending_confirmation_span_throws_should_continue_draining()
    {
        // Arrange
        const int messageCount = 2;
        var topic = new RoutingKey("Confirmation.EndSpan.Throws.Topic");
        var circuitBreaker = new InMemoryOutboxCircuitBreaker();
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = topic })
        {
            UseAsyncPublishConfirmation = true,
            PublishFailurePredicate = _ => true
        };
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { [topic] = producer });

        _ = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: new ThrowingConfirmationTracer(throwOnEndSpan: true),
            new FindPublicationByPublicationTopicOrRequestType(),
            outboxCircuitBreaker: circuitBreaker);

        using var context = TestCorrelator.CreateContext();

        // Act
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new Message(
                new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT),
                new MessageBody("test")));
        }

        await producer.DisposeAsync();

        // Assert
        var confirmationWarnings = TestCorrelator.GetLogEventsFromCurrentContext()
            .Where(logEvent => logEvent.Level == LogEventLevel.Warning)
            .Where(logEvent => logEvent.RenderMessage().Contains(topic.Value))
            .ToList();
        Assert.Equal(messageCount, confirmationWarnings.Count);
        Assert.Contains(topic, circuitBreaker.TrippedTopics);
    }
}
