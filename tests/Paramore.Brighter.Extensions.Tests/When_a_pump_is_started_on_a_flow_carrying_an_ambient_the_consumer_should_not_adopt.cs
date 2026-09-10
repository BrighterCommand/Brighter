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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// ADR 0075 bracket 3 - the consumer pump's own flow must never adopt an ambient carried by whatever
// flow started it. A Dispatcher's Receive() spawns each subscription's Performer via
// Task.Factory.StartNew, which flows ExecutionContext (and with it any AsyncLocal-backed ambient) by
// default - so without a bracket suppressing adoption inside the pump's own flow, a consumer pipeline
// would silently adopt whatever ambient happened to be live on the thread that called Receive(), which
// has nothing to do with the message being consumed (FR-19).
public class ConsumerPumpFlowSuppressionTests
{
    [Fact]
    public async Task When_a_pump_is_started_on_a_flow_carrying_an_ambient_the_consumer_should_not_adopt()
    {
        // Arrange - a JoinAmbient host, all three lifetimes Scoped, with a real ambient established on
        // the flow that will call Dispatcher.Receive() - a live, adoptable IUnitOfWork, not merely an
        // absent one, so a silent adoption would be observable
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new UnitOfWorkRecorder();
        var signal = new ConsumerPipelineSignal(expectedMessageCount: 2);
        var scopeProvider = new AsyncLocalScopeProvider();
        var capturingLoggerProvider = new CapturingLoggerProvider();

        var routingKey = new RoutingKey("consumer.pipeline");
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
        var producer = new InMemoryMessageProducer(bus, new Publication { Topic = routingKey, RequestType = typeof(ConsumerPipelineCommand) });

        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(recorder);
        services.AddSingleton(signal);
        services.AddSingleton<IAmAScopeProvider>(scopeProvider);
        services.AddLogging(builder => builder.AddProvider(capturingLoggerProvider));
        services
            .AddConsumers(options =>
            {
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("consumer-pipeline"),
                        new ChannelName("consumer-pipeline:in-memory"),
                        routingKey,
                        typeof(ConsumerPipelineCommand),
                        messagePumpType: MessagePumpType.Reactor)
                };
                options.DefaultChannelFactory = channelFactory;
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
                options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        { new ProducerKey("in-memory"), producer }
                    });
            })
            .AutoFromAssemblies();

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        // Act - establish the ambient on this flow, then start the pump on it and post two messages for
        // it to consume
        using var ambientScope = provider.CreateScope();
        var ambientUnitOfWork = ambientScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        scopeProvider.Establish(new AsyncLocalAmbientScope(ambientScope.ServiceProvider));

        dispatcher.Receive();

        var mapper = new ConsumerPipelineMapper();
        producer.Send(mapper.MapToMessage(new ConsumerPipelineCommand(), new Publication { Topic = routingKey, RequestType = typeof(ConsumerPipelineCommand) }));
        producer.Send(mapper.MapToMessage(new ConsumerPipelineCommand(), new Publication { Topic = routingKey, RequestType = typeof(ConsumerPipelineCommand) }));

        var bothProcessed = signal.WaitForAll(TimeSpan.FromSeconds(10));

        await dispatcher.End();
        scopeProvider.Clear();

        Assert.True(bothProcessed, "the consumer did not process both messages within the timeout");

        // Assert - both consumer pipelines resolved and disposed their own, fresh IUnitOfWork - neither
        // adopted the ambient still live on the flow that started the pump
        Assert.Equal(2, recorder.UnitsOfWork.Count);
        Assert.All(recorder.UnitsOfWork, unitOfWork => Assert.NotSame(ambientUnitOfWork, unitOfWork));
        Assert.All(recorder.UnitsOfWork, unitOfWork => Assert.True(unitOfWork.IsDisposed));

        // Assert - no diagnostic fired: an AlwaysNew ask never reaches the code that would warn about a
        // declined or unusable ambient
        Assert.Empty(capturingLoggerProvider.Entries.Where(e => e.Level == LogLevel.Warning));
    }
}
