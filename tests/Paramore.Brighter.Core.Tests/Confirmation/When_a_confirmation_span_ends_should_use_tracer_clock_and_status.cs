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
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    [System.Obsolete]
    public class ConfirmationSpanEndTests : IDisposable
    {
        private const string TestSource = "Paramore.Brighter.Tests";

        private readonly TracerProvider _traceProvider;
        private readonly BrighterTracer _tracer;
        private readonly ICollection<Activity> _exportedActivities;
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;
        private readonly RequestContext _requestContext = new();
        private readonly RoutingKey _topic = new("Confirmation.Span.End.Topic");

        public ConfirmationSpanEndTests()
        {
            // Arrange: a real tracer driven by a FakeTimeProvider, with an in-memory exporter, and an
            // InMemory producer whose async confirmation pump always succeeds (ack).
            _exportedActivities = new List<Activity>();
            _traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(TestSource, "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();
            _tracer = new BrighterTracer(_timeProvider);

            var bus = new InternalBus();
            _outbox = new InMemoryOutbox(_timeProvider);
            _producer = new InMemoryMessageProducer(bus, new Publication { Topic = _topic })
            {
                UseAsyncPublishConfirmation = true
                // PublishFailurePredicate not set => every confirmation succeeds (ack)
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
                tracer: _tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox: _outbox,
                timeProvider: _timeProvider);

            _message = new Message(
                new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
                new MessageBody("test"));
        }

        public void Dispose()
        {
            _traceProvider.Dispose();
            Activity.Current = null;
        }

        private Activity? ConfirmationSpan() => _exportedActivities.SingleOrDefault(a =>
            a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value == "settle")
            && a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == _message.Id.Value));

        [Test]
        public async Task When_a_confirmation_span_ends_should_use_tracer_clock_and_status()
        {
            // Arrange: outstanding message; the fake clock is never advanced during the callback.
            _outbox.Add(_message, _requestContext);

            // Act: drive the async pump to a successful confirmation; the callback emits and closes the
            // settle span, and DisposeAsync drains.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: the span is closed via the tracer, so a successful confirmation carries Ok status
            // (it would be left Unset if disposed directly).
            var confirmation = ConfirmationSpan();
            await Assert.That(confirmation).IsNotNull();
            await Assert.That(confirmation!.Status).IsEqualTo(ActivityStatusCode.Ok);

            // Assert: the end time comes from the tracer's TimeProvider, not the wall clock — with the
            // fake clock unmoved the start and end coincide, giving a near-zero duration. (Disposing the
            // Activity directly would stamp the end from the real clock, yielding a ~decades-long duration
            // against the fake 2000-epoch start.)
            await Assert.That(confirmation.Duration < TimeSpan.FromSeconds(1)).IsTrue().Because($"expected a near-zero duration from the fake clock, but was {confirmation.Duration}");
        }
    }
}