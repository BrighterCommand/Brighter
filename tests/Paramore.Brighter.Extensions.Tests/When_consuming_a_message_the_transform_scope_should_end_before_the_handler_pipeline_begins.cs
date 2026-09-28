using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformScopeEndsBeforeHandlerPipelineBeginsTests
{
    [Fact]
    public void When_consuming_a_message_the_transform_scope_should_end_before_the_handler_pipeline_begins()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. IMarker is registered
        //AddScoped and injected into both the unwrap transform (MarkerTransform) and the handler
        //(MarkerHandlerAsync) for the same MarkerCommand message
        TransformPipelineBuilderAsync.ClearPipelineCache();

        var log = new MarkerLog();
        var collection = new ServiceCollection();
        collection.AddSingleton(log);
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<MarkerMapperAsync>();
        collection.AddScoped<MarkerTransform>();
        collection.AddScoped<MarkerHandlerAsync>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var provider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MarkerCommand, MarkerHandlerAsync>();

        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            new ServiceProviderHandlerFactory(provider),
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory());

        var routingKey = new RoutingKey("markerTopic");
        var bus = new InternalBus();
        var timeProvider = new FakeTimeProvider();
        var channel = new ChannelAsync(new("markerChannel"), routingKey,
            new InMemoryMessageConsumer(routingKey, bus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

        var mapperRegistry = new MessageMapperRegistry(null, new ServiceProviderMapperFactoryAsync(provider));
        mapperRegistry.RegisterAsync<MarkerCommand, MarkerMapperAsync>();

        var messagePump = new Proactor(commandProcessor, _ => typeof(MarkerCommand), mapperRegistry,
            new ServiceProviderTransformerFactoryAsync(provider), new InMemoryRequestContextFactory(), channel)
        {
            Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
        };

        var command = new MarkerCommand();
        var message = new Message(
            new MessageHeader(command.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody("test"));

        channel.Enqueue(message);
        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));

        //act — the consumer processes the one message
        messagePump.Run();

        //assert — the transform and the handler each resolved their own IMarker from a different scope
        Assert.Single(log.TransformMarkers);
        Assert.Single(log.HandlerMarkers);
        Assert.NotSame(log.TransformMarkers[0], log.HandlerMarkers[0]);

        //assert — the transform's IMarker was already disposed by the time Handle/HandleAsync was entered
        Assert.True(log.TransformDisposedAtHandlerEntry[0]);
    }
}
