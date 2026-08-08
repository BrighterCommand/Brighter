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
using Paramore.Brighter;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    // Companion to BulkDispatchConfirmingProducerTests: locks the failure half of the bulk-dispatch
    // guard change. PublishFailurePredicate is init-only on InMemoryMessageProducer (deliberately, so
    // it can't be flipped at runtime), so this lives in its own fixture whose constructor seeds the
    // always-nack predicate via the object initializer rather than assigning it inline in the test.
    public class BulkDispatchConfirmingProducerConfirmationFailsTests
    {
        private readonly RoutingKey _topic = new("Bulk.Confirming.Producer.Topic");
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RequestContext _requestContext = new();
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InMemoryMessageProducer _producer;
        private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;

        public BulkDispatchConfirmingProducerConfirmationFailsTests()
        {
            // Arrange: an InMemory producer that is both a bulk producer and a confirming producer,
            // with its async confirmation pump enabled and every confirmation set to nack. A frozen
            // clock keeps the outbox outstanding/dispatched windows deterministic.
            var bus = new InternalBus();
            _outbox = new InMemoryOutbox(_timeProvider);
            _producer = new InMemoryMessageProducer(bus, new Publication { Topic = _topic })
            {
                UseAsyncPublishConfirmation = true,
                PublishFailurePredicate = _ => true
            };

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { [_topic] = _producer });

            // Constructing the mediator wires its OnMessagePublished callback onto the producer.
            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer: null,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox: _outbox,
                outboxCircuitBreaker: _circuitBreaker,
                timeProvider: _timeProvider);
        }

        private Message MessageOn(RoutingKey topic) => new(
            new MessageHeader(new Id(Guid.NewGuid().ToString()), topic, MessageType.MT_EVENT),
            new MessageBody("test"));

        [Fact]
        public async Task When_a_confirming_bulk_producer_confirmation_fails_does_not_dispatch_inline()
        {
            // Arrange: the producer sends successfully but every confirmation nacks. InMemory's send
            // always reports success, so `sent` is true — the only thing that could mark the message
            // dispatched is the inline bulk path, which must be skipped for a confirming producer.
            var message = MessageOn(_topic);
            _outbox.Add(message, _requestContext);

            // Act: bulk-clear, then drain the failure confirmations (the failure path has no await
            // before TripTopic, so DisposeAsync drains it to completion).
            await _mediator.ClearOutstandingFromOutboxAsync(
                amountToClear: 10, minimumAge: TimeSpan.Zero, useBulk: true, _requestContext);
            await _producer.DisposeAsync();

            // Assert: despite a successful send, the message is NOT dispatched — MarkDispatched was
            // correctly deferred to a confirmation that never acked — so it stays outstanding for the
            // Sweeper, and the failed confirmation tripped the breaker on the wire topic (FR-3). The
            // trip also proves the bulk branch was entered: the pre-change throw would have sent
            // nothing and tripped nothing.
            var dispatched = _outbox.DispatchedMessages(TimeSpan.Zero, _requestContext).ToList();
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, _requestContext).ToList();
            Assert.DoesNotContain(dispatched, m => m.Id == message.Id);
            Assert.Contains(outstanding, m => m.Id == message.Id);
            Assert.Contains(_topic, _circuitBreaker.TrippedTopics);
        }
    }
}
