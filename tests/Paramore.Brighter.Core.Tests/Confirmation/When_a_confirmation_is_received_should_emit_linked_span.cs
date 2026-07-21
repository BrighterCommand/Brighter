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
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Confirmation
{
    [System.Obsolete]
    public class ConfirmationLinkedSpanTests : IDisposable
    {
        private const string TestSource = "Paramore.Brighter.Tests";
        private readonly TracerProvider _traceProvider;
        private readonly BrighterTracer _tracer;
        private readonly ICollection<Activity> _exportedActivities;
        private readonly RoutingKey _topic = new("Confirmation.Linked.Span.Topic");

        public ConfirmationLinkedSpanTests()
        {
            _exportedActivities = new List<Activity>();
            _traceProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(TestSource, "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();

            _tracer = new BrighterTracer();
        }

        public void Dispose()
        {
            _traceProvider.Dispose();
            Activity.Current = null;
        }

        // Builds an InMemory producer whose async confirmation pump is enabled, wires the
        // mediator's OnMessagePublished callback onto it (with a real tracer), and returns the
        // producer. failWith == true forces every confirmation to fail (nack); null = always ack.
        private InMemoryMessageProducer BuildConfirmingProducer(bool fail)
        {
            var bus = new InternalBus();
            var producer = new InMemoryMessageProducer(bus, new Publication { Topic = _topic })
            {
                UseAsyncPublishConfirmation = true,
                PublishFailurePredicate = fail ? _ => true : null
            };

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { [_topic] = producer });

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
                new FindPublicationByPublicationTopicOrRequestType());

            return producer;
        }

        private Message NewMessage() => new(
            new MessageHeader(new Id(Guid.NewGuid().ToString()), _topic, MessageType.MT_EVENT),
            new MessageBody("test"));

        // The confirmation (settle) span is the standalone producer-kind span the mediator emits
        // for each confirmation; it is distinct from the publish span we start in the test. We
        // match on this message's unique id (tagged on both branches) so parallel observability
        // tests cannot contaminate it. The success branch carries no topic, so id is the only
        // discriminator available on both branches.
        private Activity? ConfirmationSpan(Id messageId) => _exportedActivities
            .SingleOrDefault(a =>
                a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingOperationType && t.Value == "settle")
                && a.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && t.Value == messageId.Value));

        [Test]
        public async Task When_a_confirmation_succeeds_should_emit_a_span_linked_to_the_publish_span()
        {
            // Arrange: a publish span is current when we send, so the pump captures its context.
            var producer = BuildConfirmingProducer(fail: false);
            var message = NewMessage();
            using var publishSpan = new ActivitySource(TestSource).StartActivity("publish");
            var publishContext = publishSpan!.Context;

            // Act: publishing drives the async pump, which raises a successful confirmation and
            // invokes the mediator callback; DisposeAsync drains the raise tasks.
            await producer.SendAsync(message);
            await producer.DisposeAsync();

            // Assert: a standalone confirmation span was emitted, linked to the publish span's
            // context, and it is a distinct span — the publish span was not reopened or mutated.
            var confirmation = ConfirmationSpan(message.Id);
            await Assert.That(confirmation).IsNotNull();
            var links = confirmation!.Links.ToArray();
            await Assert.That(links).HasSingleItem();
            await Assert.That(links[0].Context).IsEqualTo(publishContext);
            await Assert.That(confirmation.SpanId).IsNotEqualTo(publishSpan.SpanId);
        }

        [Test]
        public async Task When_a_confirmation_fails_should_emit_a_span_linked_to_the_publish_span()
        {
            // Arrange: a failing confirmation must still emit the linked span (symmetric branches).
            var producer = BuildConfirmingProducer(fail: true);
            var message = NewMessage();
            using var publishSpan = new ActivitySource(TestSource).StartActivity("publish");
            var publishContext = publishSpan!.Context;

            // Act
            await producer.SendAsync(message);
            await producer.DisposeAsync();

            // Assert: the failure-branch confirmation span links to the publish span just as the
            // success branch does, and remains a distinct span.
            var confirmation = ConfirmationSpan(message.Id);
            await Assert.That(confirmation).IsNotNull();
            var links = confirmation!.Links.ToArray();
            await Assert.That(links).HasSingleItem();
            await Assert.That(links[0].Context).IsEqualTo(publishContext);
            await Assert.That(confirmation.SpanId).IsNotEqualTo(publishSpan.SpanId);
        }

        [Test]
        public async Task When_the_publish_context_is_absent_should_emit_a_span_with_no_link()
        {
            // Arrange: no publish span is active at send time, so no context is captured.
            var producer = BuildConfirmingProducer(fail: false);
            var message = NewMessage();
            Activity.Current = null;

            // Act
            await producer.SendAsync(message);
            await producer.DisposeAsync();

            // Assert: a confirmation span is still emitted, but it degrades to no link (AC-2b).
            var confirmation = ConfirmationSpan(message.Id);
            await Assert.That(confirmation).IsNotNull();
            await Assert.That(confirmation!.Links).IsEmpty();
        }
    }
}