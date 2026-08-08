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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    [Collection("Observability")]
    public class ConfirmationSuccessNestedSpanTests : IDisposable
    {
        private const string TestSource = "Paramore.Brighter.Tests";
        private const string MarkDispatchedOperation = "mark_as_dispatched.outstanding_messages";

        private readonly TracerProvider _traceProvider;
        private readonly BrighterTracer _tracer;
        private readonly ICollection<Activity> _exportedActivities;
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InMemoryOutboxCircuitBreaker _circuitBreaker = new();
        private readonly RequestContext _requestContext = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InMemoryMessageProducer _producer;
        private readonly Message _message;
        private readonly RoutingKey _topic = new("Confirmation.Success.Nested.Topic");

        public ConfirmationSuccessNestedSpanTests()
        {
            // Arrange: a real tracer with an in-memory exporter, a real breaker, and an InMemory
            // producer whose async confirmation pump is enabled and always succeeds (acks).
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

            // Constructing the mediator wires its OnMessagePublished callback onto the producer and
            // sets the outbox tracer, so MarkDispatched emits a DB span.
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
                outboxCircuitBreaker: _circuitBreaker,
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

        [Fact]
        public async Task When_a_confirmation_succeeds_should_nest_dispatch_under_span()
        {
            // Arrange: the message is recorded in the outbox as outstanding, and a publish span is
            // current when we send so the pump captures its context for the link.
            _outbox.Add(_message, _requestContext);
            using var publishSpan = new ActivitySource(TestSource).StartActivity("publish");
            var publishContext = publishSpan!.Context;

            // Act: publishing drives the async pump, which raises a successful confirmation; the
            // callback emits the settle span, marks the message dispatched, and DisposeAsync drains.
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: delivery is frozen — the message is now marked dispatched in the outbox.
            var dispatched = _outbox.DispatchedMessages(TimeSpan.Zero, _requestContext).ToList();
            Assert.Contains(dispatched, m => m.Id == _message.Id);

            // Assert: a distinct confirmation (settle) span was emitted, linked to the publish span.
            var confirmation = ConfirmationSpan();
            Assert.NotNull(confirmation);
            Assert.NotEqual(publishSpan.SpanId, confirmation!.SpanId);
            var links = confirmation.Links.ToArray();
            Assert.Single(links);
            Assert.Equal(publishContext, links[0].Context);

            // Assert: the MarkDispatched DB span nests under the confirmation span (C-6 fix) — its
            // parent is the confirmation span's id specifically, not merely some ambient parent.
            var dbSpan = _exportedActivities.SingleOrDefault(a =>
                a.Tags.Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == MarkDispatchedOperation)
                && a.TraceId == confirmation.TraceId);
            Assert.NotNull(dbSpan);
            Assert.Equal(confirmation.SpanId, dbSpan!.ParentSpanId);
        }

        [Fact]
        public async Task When_a_confirmation_succeeds_should_not_warn_or_trip()
        {
            // Arrange: the message is recorded in the outbox as outstanding.
            _outbox.Add(_message, _requestContext);

            using var context = TestCorrelator.CreateContext();

            // Act
            await _producer.SendAsync(_message);
            await _producer.DisposeAsync();

            // Assert: the sent log is preserved at Information and no Warning is raised (AC-13).
            var logEvents = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
            Assert.Contains(logEvents, e =>
                e.Level == LogEventLevel.Information && e.RenderMessage().Contains(_message.Id.Value));
            Assert.DoesNotContain(logEvents, e => e.Level == LogEventLevel.Warning);

            // Assert: a successful confirmation never trips the breaker.
            Assert.Empty(_circuitBreaker.TrippedTopics);
        }
    }
}
