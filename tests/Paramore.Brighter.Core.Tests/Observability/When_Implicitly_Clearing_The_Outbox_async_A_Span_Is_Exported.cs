using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability;
[Collection("Observability")]
public class ImplicitClearingAsyncObservabilityTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyEvent _event;
    private readonly TracerProvider _traceProvider;
    private readonly List<Activity> _exportedActivities;

    public ImplicitClearingAsyncObservabilityTests()
    {
        const string topic = "MyEvent";
        
        IAmAnOutboxSync<Message, CommittableTransaction> outbox = new InMemoryOutbox();
        _event = new MyEvent("TestEvent");

        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyEventHandler>();

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync())
            );
        messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        
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
            .RetryAsync();
        
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

        var policyRegistry = new PolicyRegistry {{CommandProcessor.RETRYPOLICYASYNC, retryPolicy}, {CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy}};
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
            new InMemoryRequestContextFactory(),
            outbox,
            maxOutStandingMessages: -1
        );
        
        CommandProcessor.ClearServiceBus();
        _commandProcessor = new CommandProcessor(
            registry, 
            handlerFactory, 
            new InMemoryRequestContextFactory(), 
            policyRegistry, 
            bus
        );
    }

    [Fact]
    public async Task When_Clearing_Implicitly_async()
    {
        using (var activity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("RunTest"))
        {
            await _commandProcessor.DepositPostAsync(_event);
            _commandProcessor.ClearAsyncOutbox(10, 0);
        }

        //wait for Background Process
        await Task.Delay(1000);

        _traceProvider.ForceFlush();

        Assert.NotEmpty(_exportedActivities);

        var act = _exportedActivities.First(a => a.Source.Name == "Paramore.Brighter.Tests");

        Assert.NotNull(act);
        Assert.Equal(false, act.TagObjects.First(a => a.Key == "bulk").Value);
        Assert.Equal(true, act.TagObjects.First(a => a.Key == "async").Value);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
        _traceProvider?.Dispose();
    }
}
