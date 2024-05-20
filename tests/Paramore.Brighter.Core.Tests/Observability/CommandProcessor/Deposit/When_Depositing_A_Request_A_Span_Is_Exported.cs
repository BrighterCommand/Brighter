using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;
using MyEvent = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyEvent;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor.Deposit;

[Collection("Observability")]
public class CommandProcessorDepositObservabilityTests 
{
    private readonly List<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Brighter.CommandProcessor _commandProcessor;
    
    public CommandProcessorDepositObservabilityTests()
    {
        const string topic = "MyEvent";
        
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        BrighterTracer tracer = new();
       
        Brighter.CommandProcessor.ClearServiceBus();
        
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyCommandHandler>();
        
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();
        
        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
        
        TimeProvider timeProvider = new FakeTimeProvider();
        IAmAnOutboxSync<Message, CommittableTransaction> outbox = new InMemoryOutbox(timeProvider);
        
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {topic, new FakeMessageProducer{Publication = { Topic = new RoutingKey(topic), RequestType = typeof(MyEvent)}}}
        });
        
        IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry, 
            messageMapperRegistry, 
            new EmptyMessageTransformerFactory(), 
            new EmptyMessageTransformerFactoryAsync(),
            outbox,
            maxOutStandingMessages: -1
        );
        
        _commandProcessor = new Brighter.CommandProcessor(
            registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
            tracer: tracer, 
            instrumentationOptions: InstrumentationOptions.All
        );
        
        
    }

    [Fact]
    public void When_Depositing_A_Request_A_Span_Is_Exported()
    {
        //arrange
        
        //act
        
        //assert
    }
}
