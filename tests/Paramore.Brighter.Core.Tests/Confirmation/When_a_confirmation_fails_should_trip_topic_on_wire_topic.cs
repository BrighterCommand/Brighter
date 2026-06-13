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
using Paramore.Brighter;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    public class ConfirmationFailureBreakerTripTests
    {
        // The producer is registered (looked up) by its Publication topic, but the wire topic
        // that the breaker must trip is the topic carried on the message header — these differ
        // for reply / rewritten-address messages, so we keep them distinct throughout.
        private readonly RoutingKey _publicationTopic = new("Confirmation.Publication.Topic");

        private readonly InternalBus _bus = new();
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly InMemoryMessageProducer _producer;

        public ConfirmationFailureBreakerTripTests()
        {
            // Arrange: an InMemory producer whose publish confirmation always fails, wired to a
            // mediator that owns a real circuit breaker.
            _producer = new InMemoryMessageProducer(_bus, new Publication { Topic = _publicationTopic })
            {
                UseAsyncPublishConfirmation = true,
                PublishFailurePredicate = _ => true
            };

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { [_publicationTopic] = _producer });

            // Constructing the mediator wires its OnMessagePublished callback onto the producer and
            // hands it the breaker we assert against.
            _ = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                // A mapper is required by the registry but never exercised: we trigger the
                // producer confirmation directly rather than dispatching through a mapper.
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer: null,
                new FindPublicationByPublicationTopicOrRequestType(),
                outboxCircuitBreaker: _circuitBreaker);
        }

        private static Message MessageWithWireTopic(RoutingKey wireTopic) =>
            new(new MessageHeader(new Id(Guid.NewGuid().ToString()), wireTopic, MessageType.MT_EVENT),
                new MessageBody("test"));

        [Fact]
        public async Task When_a_confirmation_fails_should_trip_the_wire_topic_not_the_publication_topic()
        {
            // Arrange: a message whose wire topic (Header.Topic) differs from the Publication topic.
            var wireTopic = new RoutingKey("Confirmation.Wire.Topic");

            // Act: a failed confirmation is raised through the mediator callback.
            await _producer.SendAsync(MessageWithWireTopic(wireTopic));
            await _producer.DisposeAsync();

            // Assert: the breaker trips on the wire topic, not the Publication topic — exact parity
            // with the non-confirmation send-failure path, which trips on message.Header.Topic.
            var trippedTopics = _circuitBreaker.TrippedTopics.ToList();
            Assert.Contains(wireTopic, trippedTopics);
            Assert.DoesNotContain(_publicationTopic, trippedTopics);
        }

        [Fact]
        public async Task When_a_confirmation_fails_for_a_rewritten_topic_should_trip_the_rewritten_address()
        {
            // Arrange: a reply-style message routed to a dynamic reply address on its header.
            var replyAddress = new RoutingKey("Confirmation.Reply.0a1b2c3d");

            // Act
            await _producer.SendAsync(MessageWithWireTopic(replyAddress));
            await _producer.DisposeAsync();

            // Assert: the breaker trips the rewritten wire address carried on the result (AC-3b).
            Assert.Contains(replyAddress, _circuitBreaker.TrippedTopics);
        }

        [Fact]
        public async Task When_a_confirmation_fails_with_an_empty_topic_should_be_a_safe_no_op()
        {
            // Arrange: a message whose wire topic is empty, so the failure carries no usable topic.
            var emptyTopic = RoutingKey.Empty;

            // Act
            await _producer.SendAsync(MessageWithWireTopic(emptyTopic));
            await _producer.DisposeAsync();

            // Assert: an empty topic trips nothing — the breaker guard treats it as a safe no-op.
            Assert.Empty(_circuitBreaker.TrippedTopics);
        }
    }
}
