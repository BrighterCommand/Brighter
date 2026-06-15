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
using Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    public class ConfirmationDispatchIsolationTests
    {
        private readonly RoutingKey _topic = new("Confirmation.Dispatch.Throws.Topic");
        private readonly ThrowingOutboxCircuitBreaker _circuitBreaker = new();
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;

        public ConfirmationDispatchIsolationTests()
        {
            // Arrange: a confirmation that always fails, wired to a mediator whose circuit breaker throws
            // from TripTopic — modelling a fault in the callback's dispatch/trip work (not the observability
            // span). The tracer is null so the observability block is a no-op and the only thing that can
            // throw inside the callback is the breaker trip.
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
                tracer: null,
                new FindPublicationByPublicationTopicOrRequestType(),
                outboxCircuitBreaker: _circuitBreaker);

            _message = new Message(
                new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
                new MessageBody("test"));
        }

        [Fact]
        public async Task When_confirmation_dispatch_throws_should_isolate_and_log()
        {
            using var context = TestCorrelator.CreateContext();

            // Act: the throwing breaker faults inside the callback's dispatch/trip work. The exception must
            // be isolated locally, so DisposeAsync (which drains and awaits the raise tasks) completes
            // without rethrowing — reaching the asserts proves the producer thread was not destabilised.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: the dispatch fault was logged (at Warning — the message is left un-dispatched for the
            // Sweeper, not lost) rather than left to escape the async callback.
            var dispatchErrors = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Where(e => e.RenderMessage().Contains("Sweeper", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Single(dispatchErrors);
        }
    }
}
