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
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    public class ConfirmationFailureWarningLogTests
    {
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;
        private readonly RoutingKey _topic = new("Confirmation.Failure.Topic");

        public ConfirmationFailureWarningLogTests()
        {
            // Arrange: an InMemory producer whose publish confirmation always fails
            var bus = new InternalBus();
            _producer = new InMemoryMessageProducer(bus, new Publication { Topic = _topic })
            {
                UseAsyncPublishConfirmation = true,
                PublishFailurePredicate = _ => true
            };

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { [_topic] = _producer });

            // Constructing the mediator wires its OnMessagePublished callback onto the producer
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
                new FindPublicationByPublicationTopicOrRequestType());

            _message = new Message(
                new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
                new MessageBody("test"));
        }

        [Test]
        public async Task When_a_confirmation_fails_should_log_warning_with_id_and_topic()
        {
            // Brighter's loggers are routed to the Serilog TestCorrelator sink by the test
            // module initializer, so we capture log events within a correlation context.
            using var context = TestCorrelator.CreateContext();

            // Act: publishing drives the async pump, which raises a failed confirmation and
            // invokes the mediator's callback; DisposeAsync drains the raise tasks.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: exactly one Warning carrying the message id and the wire topic, and
            // nothing logged at Error or above (AC-11).
            var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

            var warnings = logEvents
                .Where(e => e.Level == LogEventLevel.Warning)
                .Where(e => e.RenderMessage().Contains(_message.Id.Value)
                            && e.RenderMessage().Contains(_topic.Value))
                .ToList();

            await Assert.That(warnings).HasSingleItem();
            await Assert.That((logEvents).Any(e => e.Level >= LogEventLevel.Error)).IsFalse();
        }
    }
}
