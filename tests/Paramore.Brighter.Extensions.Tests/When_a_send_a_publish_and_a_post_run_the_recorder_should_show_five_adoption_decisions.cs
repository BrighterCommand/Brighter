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

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// A host that opts in to joining an ambient scope, with every lifetime Scoped, asks its registered scope
// provider once per pipeline it builds - never once per resolution inside that pipeline, and never once
// per participant sharing that pipeline. A Send asks once, for its one handler pipeline. A Publish to
// three subscribers asks three times, once per subscriber, because each subscriber's own pipeline is
// isolated from the others and always creates and owns its own scope rather than joining whatever ambient
// the caller's flow might be running under - the ask is still made, though, which is what tells an
// isolated subscriber apart from a host with no provider registered at all, which never asks. A Post asks
// exactly once for its transform pipeline, not twice, because the mapper and its transform share the one
// pipeline scope decision.
public class AdoptionDecisionCountTests
{
    [Fact]
    public async Task When_a_send_a_publish_and_a_post_run_the_recorder_should_show_five_adoption_decisions()
    {
        // Arrange - a JoinAmbient host, all three lifetimes Scoped, with a provider that never actually
        // offers anything to adopt: this test is only about what affinity each pipeline asks for, not
        // about what happens when an ambient is actually available to join
        TransformPipelineBuilder.ClearPipelineCache();

        var recordingProvider = new RecordingScopeProvider();
        var handlerMarkerRecorder = new HandlerMarkerRecorder();
        var unitOfWorkRecorder = new UnitOfWorkRecorder();

        var collection = new ServiceCollection();
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<ScopedHandlerCommandHandler>();
        collection.AddSingleton(handlerMarkerRecorder);
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(unitOfWorkRecorder);
        collection.AddScoped<OrderPlacedHandlerOne>();
        collection.AddScoped<OrderPlacedHandlerTwo>();
        collection.AddScoped<OrderPlacedHandlerThree>();
        collection.AddScoped<AdoptionDecisionPostMapper>();
        collection.AddSingleton<IAmAScopeProvider>(recordingProvider);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
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
        mapperRegistry.Register<AdoptionDecisionPostCommand, AdoptionDecisionPostMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(AdoptionDecisionPostCommand) }) }
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

        // Act - one Send, one Publish to three subscribers, and one Post
        commandProcessor.Send(new ScopedHandlerCommand());
        await commandProcessor.PublishAsync(new OrderPlaced());
        commandProcessor.Post(new AdoptionDecisionPostCommand());

        // Assert - exactly five asks were made: the Send's own handler pipeline, each of the three
        // Publish subscribers' own pipelines, and the Post's one transform pipeline (shared by its mapper
        // and its transformer, never asked twice)
        Assert.Equal(5, recordingProvider.Asks.Count);

        // Assert - the Send's handler pipeline asked to join whatever ambient is available
        Assert.Equal(ScopeAffinity.JoinAmbient, recordingProvider.Asks[0]);

        // Assert - each Publish subscriber's own pipeline always creates and owns its own scope, never
        // asking to join an ambient, whatever affinity the host otherwise opted into
        Assert.Equal(ScopeAffinity.AlwaysNew, recordingProvider.Asks[1]);
        Assert.Equal(ScopeAffinity.AlwaysNew, recordingProvider.Asks[2]);
        Assert.Equal(ScopeAffinity.AlwaysNew, recordingProvider.Asks[3]);

        // Assert - the Post's one transform pipeline asked to join whatever ambient is available, exactly
        // once - not once for its mapper and again for its transformer
        Assert.Equal(ScopeAffinity.JoinAmbient, recordingProvider.Asks[4]);
    }
}
