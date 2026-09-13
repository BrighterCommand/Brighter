#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-14 half 1 (FR-11(a), FR-15) - a host configured exactly as before this spec, with no
// IAmAScopeProvider registered and the affinity option left at its default (AlwaysNew), behaves
// identically across every representative flow: Send, Publish, Post, DepositPost/ClearOutbox and
// consumption. Nothing here is a new invariant - each assertion mirrors what an earlier Phase 1/2 task
// already established for its own flow in isolation - this fact is the regression checkpoint that all
// five still hold together, with no ambient-scope machinery configured at all.
public class NoScopeProviderRegisteredRegressionTests
{
    [Fact]
    public async Task When_no_scope_provider_is_registered_the_existing_suite_should_pass()
    {
        // Arrange - Send, Publish, Post and DepositPost all share one host. No IAmAScopeProvider is
        // registered anywhere in it - that absence is the whole point of this fact
        TransformPipelineBuilder.ClearPipelineCache();

        var handlerMarkerRecorder = new HandlerMarkerRecorder();
        var unitOfWorkRecorder = new UnitOfWorkRecorder();
        var constructionOrderRecorder = new ConstructionOrderRecorder();

        var collection = new ServiceCollection();
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<ScopedHandlerCommandHandler>();
        collection.AddSingleton(handlerMarkerRecorder);
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(unitOfWorkRecorder);
        collection.AddScoped<OrderPlacedHandlerOne>();
        collection.AddScoped<OrderPlacedHandlerTwo>();
        collection.AddScoped<OrderPlacedHandlerThree>();
        collection.AddScoped<PostedMapper>();
        collection.AddSingleton(constructionOrderRecorder);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<ScopedHandlerCommand, ScopedHandlerCommandHandler>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerOne>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerTwo>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerThree>();

        var handlerFactory = new ServiceProviderHandlerFactory(rootProvider);

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<PostedCommand, PostedMapper>();

        var routingKey = new RoutingKey("test");
        var internalBus = new InternalBus();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(internalBus, new Publication { Topic = routingKey, RequestType = typeof(PostedCommand) }) }
        });

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            mapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(timeProvider) { Tracer = tracer }
        );

        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        // Act/Assert - Send: one fresh handler pipeline, its Scoped dependency disposed once Send returns
        commandProcessor.Send(new ScopedHandlerCommand());
        Assert.Single(handlerMarkerRecorder.Markers);
        Assert.True(handlerMarkerRecorder.Markers[0].IsDisposed);

        // Act/Assert - Publish: three subscribers, each its own fresh, disposed pipeline
        await commandProcessor.PublishAsync(new OrderPlaced());
        Assert.Equal(3, unitOfWorkRecorder.UnitsOfWork.Count);
        Assert.Equal(3, unitOfWorkRecorder.UnitsOfWork.Distinct().Count());
        Assert.All(unitOfWorkRecorder.UnitsOfWork, unitOfWork => Assert.True(unitOfWork.IsDisposed));

        // Act/Assert - Post: one fresh, disposed mapper for the transform pipeline, and the message
        // reaches the wire immediately
        commandProcessor.Post(new PostedCommand());
        Assert.Equal(new[] { "Constructed:1", "Disposed:1" }, constructionOrderRecorder.Events);
        Assert.Single(internalBus.Stream(routingKey));

        // Act/Assert - DepositPost: its own fresh, disposed mapper, but the message does not reach the
        // wire until ClearOutbox is called separately
        var depositedId = commandProcessor.DepositPost(new PostedCommand());
        Assert.Equal(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, constructionOrderRecorder.Events);
        Assert.Single(internalBus.Stream(routingKey));

        commandProcessor.ClearOutbox(new[] { depositedId });
        Assert.Equal(2, internalBus.Stream(routingKey).Count());

        // Arrange - consumption: a second, independent host wired through AddConsumers, again with no
        // IAmAScopeProvider registered, driving a real Dispatcher/Performer pump over one message
        var consumerUnitOfWorkRecorder = new UnitOfWorkRecorder();
        var signal = new ConsumerPipelineSignal(expectedMessageCount: 1);
        var capturingLoggerProvider = new CapturingLoggerProvider();

        // ConsumerPipelineMapper hardcodes this routing key on every message it maps, so the
        // subscription below must listen on the same value for the message to ever be delivered
        var consumerRoutingKey = new RoutingKey("consumer.pipeline");
        var consumerBus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(consumerBus, TimeProvider.System);
        var consumerProducer = new InMemoryMessageProducer(consumerBus, new Publication { Topic = consumerRoutingKey, RequestType = typeof(ConsumerPipelineCommand) });

        var consumerServices = new ServiceCollection();
        consumerServices.AddScoped<IUnitOfWork, UnitOfWork>();
        consumerServices.AddSingleton(consumerUnitOfWorkRecorder);
        consumerServices.AddSingleton(signal);
        consumerServices.AddLogging(builder => builder.AddProvider(capturingLoggerProvider));
        consumerServices
            .AddConsumers(options =>
            {
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("consumer-pipeline-no-provider"),
                        new ChannelName("consumer-pipeline-no-provider:in-memory"),
                        consumerRoutingKey,
                        typeof(ConsumerPipelineCommand),
                        messagePumpType: MessagePumpType.Reactor)
                };
                options.DefaultChannelFactory = channelFactory;
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        { new ProducerKey("in-memory"), consumerProducer }
                    });
            })
            .AutoFromAssemblies();

        var consumerProvider = consumerServices.BuildServiceProvider();
        var dispatcher = consumerProvider.GetRequiredService<IDispatcher>();

        dispatcher.Receive();

        var consumerMapper = new ConsumerPipelineMapper();
        consumerProducer.Send(consumerMapper.MapToMessage(new ConsumerPipelineCommand(), new Publication { Topic = consumerRoutingKey, RequestType = typeof(ConsumerPipelineCommand) }));

        var processed = signal.WaitForAll(TimeSpan.FromSeconds(10));
        await dispatcher.End();

        // Assert - consumption completed, resolved and disposed its own fresh Scoped dependency, and
        // logged nothing at Warning or above - FR-24's diagnostics never fire with no provider registered
        Assert.True(processed, "the consumer did not process the message within the timeout");
        Assert.Single(consumerUnitOfWorkRecorder.UnitsOfWork);
        Assert.True(consumerUnitOfWorkRecorder.UnitsOfWork[0].IsDisposed);
        Assert.DoesNotContain(capturingLoggerProvider.Entries, entry => entry.Level >= LogLevel.Warning);
    }
}
