﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
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
    private readonly IAmAnOutboxSync<Message> _outbox;
    private readonly MyEvent _event;
    private readonly TracerProvider _traceProvider;
    private readonly List<Activity> _exportedActivities;

    public ImplicitClearingAsyncObservabilityTests()
    {
        _outbox = new InMemoryOutbox();
        _event = new MyEvent("TestEvent");

        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyEventHandler>();

        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()));
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
        
        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

        var policyRegistry = new PolicyRegistry {{CommandProcessor.RETRYPOLICY, retryPolicy}, {CommandProcessor.RETRYPOLICYASYNC, circuitBreakerPolicy}};
        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {MyEvent.Topic, new FakeMessageProducer()}
        });
        producerRegistry.GetDefaultProducer().MaxOutStandingMessages = -1;
        
        CommandProcessor.ClearExtServiceBus();
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            policyRegistry, messageMapperRegistry,_outbox,producerRegistry);
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
        CommandProcessor.ClearExtServiceBus();
        _traceProvider?.Dispose();
    }
}
