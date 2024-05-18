using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor;
[Collection("Observability")]
public class ImplicitClearingObservabilityTests : IDisposable
{
    private readonly Brighter.CommandProcessor _commandProcessor;
    private readonly MyEvent _event;
    private readonly TracerProvider _traceProvider;
    private readonly List<Activity> _exportedActivities;
    private readonly TimeProvider _timeProvider;

    public ImplicitClearingObservabilityTests()
    {
        const string topic = "MyEvent";

        _timeProvider = new FakeTimeProvider();
        IAmAnOutboxSync<Message, CommittableTransaction> outbox = new InMemoryOutbox(_timeProvider);
        _event = new MyEvent("TestEvent");

        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyEventHandler>();

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
            null);
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        
        var container = new ServiceCollection();
        container.AddTransient<MyEvent>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        
        _traceProvider = builder.AddSource("Paramore.Brighter.*")
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry {{Brighter.CommandProcessor.RETRYPOLICY, retryPolicy}};
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

        Brighter.CommandProcessor.ClearServiceBus();
        
        _commandProcessor = new Brighter.CommandProcessor(
            registry, 
            handlerFactory, 
            new InMemoryRequestContextFactory(),
            policyRegistry, 
            bus
        );
    }

    [Fact]
    public async Task When_Clearing_Implicitly()
    {
        using (var activity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("RunTest"))
        {
            _commandProcessor.DepositPost(_event);
            _commandProcessor.ClearOutbox(10, 0);
        }

        await Task.Delay(100); //Allow time for clear to run

        _traceProvider.ForceFlush();
        
        Assert.NotEmpty(_exportedActivities);

        var act = _exportedActivities.First(a => a.Source.Name == "Paramore.Brighter.Tests");

        Assert.NotNull(act);
        Assert.Equal(false,act.TagObjects.First(a => a.Key == "bulk").Value);
        Assert.Equal(false,act.TagObjects.First(a => a.Key == "async").Value);
    }

    public void Dispose()
    {
        Brighter.CommandProcessor.ClearServiceBus();
        _traceProvider?.Dispose();
    }
}
