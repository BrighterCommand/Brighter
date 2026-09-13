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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-20 (FR-19) - the affinity flag is inert on the consumer side. A consumer pipeline's ask always
// carries AlwaysNew (C-14), so FR-24.2's "no ambient offered" diagnostic - evaluated on JoinAmbient
// asks only - is never reached whatever the extension's own affinity argument says, and neither is
// FR-23's. Two facts, each driving a real ServiceActivatorHostedService/Dispatcher/Performer pump over
// a hundred messages through a mixed producer-and-consumer host - AddBrighter(...) registered before
// AddConsumers(Action<ConsumersOptions>) (C-12) - once with AddBrighterRequestScope(AlwaysNew), once
// with AddBrighterRequestScope(JoinAmbient). Both must show the identical, fixed shape: a hundred
// distinct, disposed mapper/transform/handler instances and no Warning - proving the flag makes no
// observable difference on this side.
public class MixedHostConsumerAffinityInertTests
{
    private const int MessageCount = 100;

    [Fact]
    public async Task When_a_mixed_host_consumes_messages_the_affinity_flag_should_be_inert()
    {
        var result = await ConsumeMessagesAsync(ScopeAffinity.AlwaysNew);
        AssertConsumptionWasUnaffected(result);
    }

    [Fact]
    public async Task When_the_host_opts_into_join_ambient_the_consumer_outcome_should_be_unchanged()
    {
        var result = await ConsumeMessagesAsync(ScopeAffinity.JoinAmbient);
        AssertConsumptionWasUnaffected(result);
    }

    private static void AssertConsumptionWasUnaffected(ConsumptionResult result)
    {
        Assert.True(result.AllMessagesProcessed, "not every message was processed within the timeout");

        // Assert - a hundred distinct, freshly-resolved instances of each pipeline component - the
        // consumer ask carries AlwaysNew whatever the host's affinity says (C-14), so nothing is ever
        // adopted here
        Assert.Equal(MessageCount, result.Mappers.Distinct().Count());
        Assert.Equal(MessageCount, result.Transforms.Distinct().Count());
        Assert.Equal(MessageCount, result.Handlers.Distinct().Count());

        // Assert - every instance was disposed at the end of its own pipeline - so is the pipeline
        // scope that produced it, since the container disposes a Scoped instance only when the scope
        // that resolved it closes
        Assert.All(result.Mappers, mapper => Assert.True(mapper.IsDisposed));
        Assert.All(result.Transforms, transform => Assert.True(transform.IsDisposed));
        Assert.All(result.Handlers, handler => Assert.True(handler.IsDisposed));

        // Assert - nothing was logged at Warning or above; FR-24's diagnostics are JoinAmbient-only and
        // the consumer ask never carries that affinity
        Assert.DoesNotContain(result.LogEntries, entry => entry.Level >= LogLevel.Warning);
    }

    private static async Task<ConsumptionResult> ConsumeMessagesAsync(ScopeAffinity requestScopeAffinity)
    {
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new MixedHostConsumerRecorder();
        var countdown = new CountdownEvent(MessageCount);
        var capturingLoggerProvider = new CapturingLoggerProvider();

        var routingKey = new RoutingKey("mixed-host.consumer");
        var bus = new InternalBus();
        var channelFactory = new InMemoryChannelFactory(bus, TimeProvider.System);
        var producer = new InMemoryMessageProducer(bus, new Publication { Topic = routingKey, RequestType = typeof(MixedHostConsumerCommand) });

        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddSingleton(countdown);
        services.AddLogging(builder => builder.AddProvider(capturingLoggerProvider));

        // The transform is resolved directly by type, not looked up via a mapper/handler registry, so
        // the built-in assembly scan's own Transient registration must be pre-empted with the Scoped
        // lifetime this test needs (T1.10/T1.13's own convention)
        services.AddScoped<MixedHostConsumerTransform>();

        // The extension's own affinity argument, never assigned on either options object alongside it
        // (D18)
        services.AddBrighterRequestScope(requestScopeAffinity);

        // AddBrighter registered before AddConsumers(Action<ConsumersOptions>) - so the producer's own
        // options object remains the registered IBrighterOptions (C-12)
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });

        services
            .AddConsumers(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("mixed-host-consumer"),
                        new ChannelName("mixed-host-consumer:in-memory"),
                        routingKey,
                        typeof(MixedHostConsumerCommand),
                        messagePumpType: MessagePumpType.Reactor)
                };
                options.DefaultChannelFactory = channelFactory;
            })
            .AddProducers(configure =>
            {
                configure.ProducerRegistry = new ProducerRegistry(
                    new Dictionary<ProducerKey, IAmAMessageProducer>
                    {
                        { new ProducerKey("in-memory"), producer }
                    });
            });

        services.AddHostedService<ServiceActivatorHostedService>();

        await using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetRequiredService<IHostedService>();

        await hostedService.StartAsync(CancellationToken.None);

        for (var i = 0; i < MessageCount; i++)
        {
            var command = new MixedHostConsumerCommand();
            producer.Send(new Message(
                new MessageHeader(command.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody("{}")));
        }

        var allProcessed = countdown.Wait(TimeSpan.FromSeconds(30));

        await hostedService.StopAsync(CancellationToken.None);

        return new ConsumptionResult(
            allProcessed,
            recorder.Mappers,
            recorder.Transforms,
            recorder.Handlers,
            capturingLoggerProvider.Entries);
    }

    private sealed record ConsumptionResult(
        bool AllMessagesProcessed,
        IReadOnlyList<MixedHostConsumerMapper> Mappers,
        IReadOnlyList<MixedHostConsumerTransform> Transforms,
        IReadOnlyList<MixedHostConsumerCommandHandler> Handlers,
        IReadOnlyCollection<CapturedLogEntry> LogEntries);
}
