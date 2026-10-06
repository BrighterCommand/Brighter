#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class OutboxResilienceRegistryTests
{
    public static TheoryData<bool, bool, bool, bool, bool, bool> RegistryCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, bool, bool, bool>();
            foreach (var useProducerFactory in new[] { false, true })
                foreach (var sendAsync in new[] { false, true })
                    foreach (var resolveMediatorFirst in new[] { false, true })
                    {
                        foreach (var addConsumers in new[] { false, true })
                            foreach (var useOptionsFactory in new[] { false, true })
                                cases.Add(addConsumers, useOptionsFactory, useProducerFactory, sendAsync, resolveMediatorFirst, false);
                        cases.Add(false, false, useProducerFactory, sendAsync, resolveMediatorFirst, true);
                    }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RegistryCases))]
    public async Task When_setting_the_options_registry_should_apply_the_pipeline_to_outbox_sends(
        bool addConsumers, bool useOptionsFactory, bool useProducerFactory,
        bool sendAsync, bool resolveMediatorFirst, bool postConfigure)
    {
        // Arrange
        var retries = 0;
        var sendAttempts = 0;
        var registry = new ResiliencePipelineRegistry<string>();
        registry.TryAddBuilder(CommandProcessor.OutboxProducer, (pipeline, _) =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero,
                OnRetry = _ =>
                {
                    retries++;
                    return default;
                }
            }));
        registry.AddBrighterDefault();

        var services = new ServiceCollection();
        var optionsRegistry = postConfigure ? null : registry;
        var builder = addConsumers
            ? useOptionsFactory
                ? services.AddConsumers(_ => new ConsumersOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddConsumers(options => { options.ResiliencePipelineRegistry = optionsRegistry; })
            : useOptionsFactory
                ? services.AddBrighter(_ => new BrighterOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddBrighter(options => { options.ResiliencePipelineRegistry = optionsRegistry; });
        if (postConfigure)
            services.PostConfigure<BrighterOptions>(options => { options.ResiliencePipelineRegistry = registry; });

        var bus = new InternalBus();
        var topic = new RoutingKey("shared-registry.event");
        var producer = new InMemoryMessageProducer(bus,
            new Publication { Topic = topic, RequestType = typeof(DefaultMapperEvent) },
            InstrumentationOptions.All)
        {
            PublishFailurePredicate = _ =>
            {
                sendAttempts++;
                if (sendAttempts == 1)
                    throw new InvalidOperationException("The first send attempt fails.");
                return false;
            }
        };
        var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            [topic] = producer
        });
        if (useProducerFactory)
            builder.AddProducers(_ => new ProducersConfiguration { ProducerRegistry = producers });
        else
            builder.AddProducers(options => { options.ProducerRegistry = producers; });
        builder.MapperRegistry(_ => { });

        await using var provider = services.BuildServiceProvider();
        if (resolveMediatorFirst)
            provider.GetRequiredService<IAmAnOutboxProducerMediator>();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();

        // Act
        if (sendAsync)
            await processor.PostAsync(new DefaultMapperEvent());
        else
            processor.Post(new DefaultMapperEvent());

        // Assert
        Assert.Single(bus.Stream(topic));
        Assert.Equal(2, sendAttempts);
        Assert.Equal(1, retries);
        Assert.Same(registry, provider.GetRequiredService<IBrighterOptions>().ResiliencePipelineRegistry);
    }
}
