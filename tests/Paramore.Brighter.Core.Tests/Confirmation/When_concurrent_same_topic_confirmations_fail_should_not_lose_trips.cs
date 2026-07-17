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
using System.Threading;
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
    public class ConcurrentConfirmationFailureTripTests
    {
        // Modest fan-out so the thread pool can run every raise concurrently; the barrier rendezvous
        // would deadlock (and the test would time out) if any raise were lost before completing.
        private const int ConcurrentFailures = 5;

        private readonly RoutingKey _topic = new("Confirmation.Concurrent.Failure.Topic");
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly InMemoryMessageProducer _producer;
        private readonly Barrier _barrier = new(ConcurrentFailures);

        public ConcurrentConfirmationFailureTripTests()
        {
            // Arrange: a producer whose confirmations always fail, wired to a mediator whose tracer
            // gates every callback on a barrier so all failures trip the breaker concurrently.
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
                tracer: new GatingConfirmationTracer(_barrier),
                new FindPublicationByPublicationTopicOrRequestType(),
                outboxCircuitBreaker: _circuitBreaker);
        }

        private Message NewMessage() => new(
            new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
            new MessageBody("test"));

        [Fact]
        public async Task When_concurrent_same_topic_confirmations_fail_should_not_lose_trips()
        {
            using var context = TestCorrelator.CreateContext();

            // Act: send N messages on the same topic; their failed confirmations rendezvous on the
            // barrier and then trip the breaker concurrently. DisposeAsync drains and awaits them.
            for (var i = 0; i < ConcurrentFailures; i++)
                await _producer.SendAsync(NewMessage());
            await _producer.DisposeAsync();

            // Assert: every concurrent failure ran to completion — N Warnings were logged (none lost
            // to the race) and the topic ends tripped (order-independent end state).
            var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Where(e => e.RenderMessage().Contains(_topic.Value))
                .ToList();
            Assert.Equal(ConcurrentFailures, warnings.Count);

            Assert.Contains(_topic, _circuitBreaker.TrippedTopics);
        }
    }
}
