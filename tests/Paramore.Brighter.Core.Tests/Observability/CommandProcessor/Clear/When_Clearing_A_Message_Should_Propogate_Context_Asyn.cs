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
using Paramore.Brighter.Core.Tests.CommandProcessors.Post;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Baggage = OpenTelemetry.Baggage;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Clear;
[NotInParallel]
public class AsyncMessageDispatchPropogateContextTests
{
    private readonly List<Activity> _exportedActivities = [];
    private readonly TracerProvider _traceProvider;
    private readonly InternalBus _internalBus = new();
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly RoutingKey _routingKey;
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;
    public AsyncMessageDispatchPropogateContextTests()
    {
        _routingKey = new RoutingKey("MyEvent");
        var builder = Sdk.CreateTracerProviderBuilder();
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        var registry = new SubscriberRegistry();
        var handlerFactory = new PostCommandTests.EmptyHandlerFactorySync();
        var retryPolicy = Policy.Handle<Exception>().RetryAsync();
        var policyRegistry = new PolicyRegistry
        {
            {
                Brighter.CommandProcessor.RETRYPOLICYASYNC,
                retryPolicy
            }
        };
        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        InMemoryOutbox outbox = new(timeProvider)
        {
            Tracer = tracer
        };
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var type = new CloudEventsType("io.goparamore.brighter.myevent");
        InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Source = new Uri("http://localhost"), RequestType = typeof(MyEvent), Topic = _routingKey, Type = type, });
        var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> { { new ProducerKey(_routingKey, type), messageProducer } });
        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, new ResiliencePipelineRegistry<string>().AddBrighterDefault(), messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), outbox, maxOutStandingMessages: -1);
        _commandProcessor = new Brighter.CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), _mediator, new InMemorySchedulerFactory(), tracer: tracer, instrumentationOptions: InstrumentationOptions.All);
    }

    [Test]
    public async Task When_Producing_A_Message_Should_Propagate_Context()
    {
        //arrange
        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("MessageDispatchPropogateContextTests");
        var traceStateString = parentActivity.TraceStateString ?? "";
        traceStateString += "test=value";
        parentActivity.TraceStateString = traceStateString;
        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("key2", "value2");
        var @event = new MyEvent();
        var context = new RequestContext
        {
            Span = parentActivity
        };
        //act
        var messageId = await _commandProcessor.DepositPostAsync(@event, context);
        //reset the parent span as deposit and clear are siblings
        context.Span = parentActivity;
        await _commandProcessor.ClearOutboxAsync([messageId], context);
        await Assert.That(() => _internalBus.Stream(_routingKey).Any(m => m.Id == messageId))
            .Eventually(src => src.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
        parentActivity?.Stop();
        _traceProvider.ForceFlush();
        //assert 
        var messages = _internalBus.Stream(_routingKey);
        var message = messages.FirstOrDefault(m => m.Id == messageId);
        await Assert.That(message).IsNotNull();
        await Assert.That(message.Header.TraceParent).IsNotNull();
        //? What is tracestate 
        await Assert.That(message.Header.TraceState?.Value).IsEqualTo(traceStateString);
        await Assert.That(message.Header.Baggage.ToString()).IsEqualTo("key=value,key2=value2");
    }
}