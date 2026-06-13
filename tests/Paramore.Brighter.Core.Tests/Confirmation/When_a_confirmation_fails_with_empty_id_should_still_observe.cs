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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    [Collection("Observability")]
    public class ConfirmationFailureEmptyIdTests : IDisposable
    {
        private const string TestSource = "Paramore.Brighter.Tests";
        private readonly TracerProvider _traceProvider;
        private readonly BrighterTracer _tracer;
        private readonly ICollection<Activity> _exportedActivities;
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly RoutingKey _topic = new("Confirmation.Failure.EmptyId.Topic");
        private readonly InMemoryMessageProducer _producer;

        public ConfirmationFailureEmptyIdTests()
        {
            // Arrange: a real tracer with an in-memory exporter, a real breaker, and an InMemory
            // producer whose publish confirmation always fails.
            _exportedActivities = new List<Activity>();
            _traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(TestSource, "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();
            _tracer = new BrighterTracer();

            var bus = new InternalBus();
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
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => throw new NotImplementedException()),
                    new SimpleMessageMapperFactoryAsync(_ => throw new NotImplementedException())),
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer: _tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outboxCircuitBreaker: _circuitBreaker);
        }

        public void Dispose()
        {
            _traceProvider.Dispose();
            Activity.Current = null;
        }

        [Fact]
        public async Task When_a_confirmation_fails_with_empty_id_should_still_observe()
        {
            // Arrange: a failing confirmation for a message carrying no id at all (Id.Empty). The
            // wire topic is still present, so the breaker has something to trip.
            var message = new Message(
                new MessageHeader(Id.Empty, _topic, MessageType.MT_EVENT),
                new MessageBody("test"));

            using var context = TestCorrelator.CreateContext();

            // Act: reaching the line after DisposeAsync proves the degenerate id did not crash the
            // callback (DisposeAsync drains and would resurface any escaped exception).
            await _producer.SendAsync(message);
            await _producer.DisposeAsync();

            // Assert (1): a Warning was still logged for the failed confirmation, naming the topic;
            // the id renders as the empty marker rather than crashing the log call.
            var warnings = TestCorrelator.GetLogEventsFromCurrentContext()
                .Where(e => e.Level == LogEventLevel.Warning)
                .Where(e => e.RenderMessage().Contains(_topic.Value))
                .ToList();
            Assert.Single(warnings);

            // Assert (2): the confirmation (settle) span was still emitted for this topic, recording
            // the missing id as the explicit "unknown" marker (not an empty string).
            var confirmationSpan = _exportedActivities.SingleOrDefault(a =>
                a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value == "settle")
                && a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value == _topic.Value));
            Assert.NotNull(confirmationSpan);
            Assert.Contains(confirmationSpan!.Tags,
                t => t.Key == BrighterSemanticConventions.MessageId && t.Value == "unknown");

            // Assert (3): the breaker still tripped on the present wire topic.
            Assert.Contains(_topic, _circuitBreaker.TrippedTopics);
        }
    }
}
