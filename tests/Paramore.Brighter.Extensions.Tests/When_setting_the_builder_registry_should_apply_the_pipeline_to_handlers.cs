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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class BuilderResilienceRegistryTests
{
    public static TheoryData<bool, bool, bool, bool> RegistryCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, bool>();
            foreach (var addConsumers in new[] { false, true })
                foreach (var useOptionsFactory in new[] { false, true })
                    foreach (var addProducers in new[] { false, true })
                        foreach (var configureOptions in new[] { false, true })
                            cases.Add(addConsumers, useOptionsFactory, addProducers, configureOptions);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RegistryCases))]
    public async Task When_setting_the_builder_registry_should_apply_the_pipeline_to_handlers(
        bool addConsumers, bool useOptionsFactory, bool addProducers, bool configureOptions)
    {
        // Arrange
        var registry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        registry.TryAddBuilder("shared-registry-retry", (pipeline, _) =>
            pipeline.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                Delay = TimeSpan.Zero
            }));

        var optionsRegistry = configureOptions
            ? new ResiliencePipelineRegistry<string>().AddBrighterDefault()
            : null;
        var services = new ServiceCollection();
        var builder = addConsumers
            ? useOptionsFactory
                ? services.AddConsumers(_ => new ConsumersOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddConsumers(options => { options.ResiliencePipelineRegistry = optionsRegistry; })
            : useOptionsFactory
                ? services.AddBrighter(_ => new BrighterOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddBrighter(options => { options.ResiliencePipelineRegistry = optionsRegistry; });
        builder.AsyncHandlersFromAssemblies([typeof(SharedRegistryCommand).Assembly]);

        if (addProducers)
            builder.AddProducers(_ => { });

        builder.ResiliencePolicyRegistry = registry;
        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new SharedRegistryCommand();

        // Act
        await processor.SendAsync(command);

        // Assert
        Assert.Equal(2, command.HandleCount);
        Assert.Same(registry, provider.GetRequiredService<IBrighterOptions>().ResiliencePipelineRegistry);
    }
}
