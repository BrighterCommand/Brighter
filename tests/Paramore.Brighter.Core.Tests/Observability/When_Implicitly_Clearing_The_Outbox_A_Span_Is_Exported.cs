﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
public class ImplicitClearingObservabilityTests
{
    private readonly CommandProcessor _commandProcessor;
    private readonly IAmAnOutboxSync<Message> _outbox;
    private readonly MyEvent _event;
    private readonly TracerProvider _traceProvider;
    private readonly List<Activity> _exportedActivities;

    public ImplicitClearingObservabilityTests()
    {
        _outbox = new InMemoryOutbox();
        _event = new MyEvent("TestEvent");

        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyEventHandler>();

        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()));
        messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
        
        var container = new ServiceCollection();
        container.AddTransient<MyEvent>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions() {HandlerLifetime = ServiceLifetime.Transient});

        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();
        
        _traceProvider = builder.AddSource("Brighter")
            .AddInMemoryExporter(_exportedActivities)
            .Build();

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var policyRegistry = new PolicyRegistry() {{CommandProcessor.RETRYPOLICY, retryPolicy}};
        var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>()
        {
            {MyEvent.Topic, new FakeMessageProducer()}
        });
        producerRegistry.GetDefaultProducer().MaxOutStandingMessages = -1;
        
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
            policyRegistry, messageMapperRegistry,_outbox,producerRegistry);
    }

    [Fact]
    public void When_Clearing_Implicitly()
    {
        _commandProcessor.DepositPost(_event);
        _commandProcessor.ClearOutbox(10, 0);

        _traceProvider.ForceFlush();
        
        Assert.NotEmpty(_exportedActivities);

        var act = _exportedActivities.First(a => a.Source.Name == "Brighter");

        Assert.NotNull(act);
        Assert.Equal(false,act.TagObjects.First(a => a.Key == "bulk").Value);
        Assert.Equal(false,act.TagObjects.First(a => a.Key == "async").Value);
    }
    
    [Fact]
    public void When_Clearing_Implicitly_async()
    {
        _commandProcessor.DepositPost(_event);
        _commandProcessor.ClearAsyncOutbox(10, 0);

        _traceProvider.ForceFlush();
        
        Assert.NotEmpty(_exportedActivities);

        var act = _exportedActivities.First(a => a.Source.Name == "Brighter");

        Assert.NotNull(act);
        Assert.Equal(false ,act.TagObjects.First(a => a.Key == "bulk").Value);
        Assert.Equal(true ,act.TagObjects.First(a => a.Key == "async").Value);
    }
}