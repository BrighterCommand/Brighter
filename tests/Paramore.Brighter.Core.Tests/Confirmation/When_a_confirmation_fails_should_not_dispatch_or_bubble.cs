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
using Paramore.Brighter.Extensions;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    public class ConfirmationFailureNoDispatchTests
    {
        private readonly RoutingKey _topic = new("Confirmation.Failure.NoDispatch.Topic");
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RequestContext _requestContext = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;

        public ConfirmationFailureNoDispatchTests()
        {
            // Arrange: an InMemory producer whose publish confirmation always fails, wired to a
            // mediator that owns a real outbox. A frozen clock keeps the outbox time windows
            // deterministic.
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
                outbox: _outbox,
                timeProvider: _timeProvider);

            _message = new Message(
                new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
                new MessageBody("test"));
        }

        [Test]
        public async Task When_a_confirmation_fails_should_not_dispatch_or_bubble()
        {
            // Arrange: the message is recorded in the outbox as outstanding (un-dispatched), as it
            // would be after a normal Add during dispatch.
            _outbox.Add(_message, _requestContext);

            // Act: a failed confirmation is raised through the mediator callback. The awaited
            // DisposeAsync drains the raise task, so any exception escaping the callback would
            // surface here.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: the failure path never marked the message dispatched — it is absent from the
            // dispatched set and remains outstanding (Sweeper-eligible). Reaching this point also
            // proves no exception bubbled out of the callback.
            var dispatched = _outbox.DispatchedMessages(TimeSpan.Zero, _requestContext).ToList();
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, _requestContext).ToList();

            await Assert.That((dispatched).Any(m => m.Id == _message.Id)).IsFalse();
            await Assert.That((outstanding).Any(m => m.Id == _message.Id)).IsTrue();
        }
    }
}
