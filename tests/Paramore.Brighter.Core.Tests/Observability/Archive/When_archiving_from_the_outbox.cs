using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Archive;

public class ExternalServiceBusArchiveObservabilityTests
{
    private readonly List<Activity> _exportedActivities = new();
    private readonly ExternalBusService<Message,CommittableTransaction> _bus;
    private readonly Publication _publication;
    private readonly FakeTimeProvider _timeProvider;
    private RoutingKey _routingKey = new("MyEvent");
    private readonly InMemoryOutbox _outbox;

    public ExternalServiceBusArchiveObservabilityTests()
    {
        IAmABus internalBus = new InternalBus();
        _timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(_timeProvider);

        var builder = Sdk.CreateTracerProviderBuilder();

        var traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        Brighter.CommandProcessor.ClearServiceBus();

        _publication = new Publication
        {
            Source = new Uri("http://localhost"),
            RequestType = typeof(MyEvent),
            Topic = _routingKey,
            Type = nameof(MyEvent),
        };

        var producer = new InMemoryProducer(internalBus, _timeProvider)
        {
            Publication = _publication
        };

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer } });

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry { { Brighter.CommandProcessor.RETRYPOLICY, retryPolicy } };

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };
        var archiveProvider = new InMemoryArchiveProvider();

        _bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            _outbox,
            archiveProvider,
            timeProvider:_timeProvider);
    }

    [Fact]
    public void When_archiving_from_the_outbox()
    {
        var context = new RequestContext();
        
        //add and clear message
        var myEvent = new MyEvent();
        var myMessage = new MyEventMessageMapper().MapToMessage(myEvent, _publication);
        _bus.AddToOutbox(myMessage, context); 
        _bus.ClearOutbox([myMessage.Id], context);
        
        //se should have an entry in the outbox
        _outbox.EntryCount.Should().Be(1);
        
        //allow time to pass
        _timeProvider.Advance(TimeSpan.FromSeconds(300)); 
        
        //archive
        _bus.Archive(TimeSpan.FromSeconds(100), context);
        
        //should be no messages in the outbox
        _outbox.EntryCount.Should().Be(0);
        
    }
}
