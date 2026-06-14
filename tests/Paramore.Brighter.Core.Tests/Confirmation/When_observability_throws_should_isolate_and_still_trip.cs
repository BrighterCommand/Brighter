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
using Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    public class ConfirmationObservabilityIsolationTests
    {
        private readonly RoutingKey _topic = new("Confirmation.Observability.Throws.Topic");
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;

        public ConfirmationObservabilityIsolationTests()
        {
            // Arrange: a confirmation that always fails, wired to a mediator whose tracer throws from
            // CreateConfirmationSpan — modelling an observability fault inside the callback.
            var bus = new InternalBus();
            _producer = new InMemoryMessageProducer(bus, new Publication { Topic = _topic })
            {
                UseAsyncPublishConfirmation = true,
                PublishFailurePredicate = _ => true
            };

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { [_topic] = _producer });

            _ = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer: new ThrowingConfirmationTracer(),
                new FindPublicationByPublicationTopicOrRequestType(),
                outboxCircuitBreaker: _circuitBreaker);

            _message = new Message(
                new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
                new MessageBody("test"));
        }

        [Fact]
        public async Task When_observability_throws_should_isolate_and_still_trip()
        {
            using var context = TestCorrelator.CreateContext();

            // Act: the throwing tracer faults inside the callback. The exception must be isolated, so
            // DisposeAsync (which drains and awaits the raise tasks) completes without rethrowing.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: the observability fault did not destabilise the failure handling — the breaker
            // still tripped the wire topic and the Warning was still logged (AC-14).
            Assert.Contains(_topic, _circuitBreaker.TrippedTopics);

            var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Where(e => e.RenderMessage().Contains(_topic.Value))
                .ToList();
            Assert.Single(warnings);
        }
    }
}
