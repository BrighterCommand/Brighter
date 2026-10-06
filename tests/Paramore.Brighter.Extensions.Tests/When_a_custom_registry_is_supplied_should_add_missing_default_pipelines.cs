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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public sealed class SuppliedRegistryDefaultsTests
{
    public static TheoryData<bool, bool, bool, string?> RegistryCases
    {
        get
        {
            var cases = new TheoryData<bool, bool, bool, string?>();
            foreach (var addConsumers in new[] { false, true })
                foreach (var useOptionsFactory in new[] { false, true })
                    foreach (var setBuilderRegistry in new[] { false, true })
                        foreach (var configuredPipeline in new[] { null, CommandProcessor.OutboxProducer, CommandProcessor.RequestReply })
                            cases.Add(addConsumers, useOptionsFactory, setBuilderRegistry, configuredPipeline);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RegistryCases))]
    public void When_a_custom_registry_is_supplied_should_add_missing_default_pipelines(
        bool addConsumers, bool useOptionsFactory, bool setBuilderRegistry, string? configuredPipeline)
    {
        // Arrange
        var registry = new ResiliencePipelineRegistry<string>();
        ResiliencePipeline? customPipeline = null;
        if (configuredPipeline is not null)
        {
            registry.TryAddBuilder(configuredPipeline, (pipeline, _) =>
                pipeline.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero
                }));
            customPipeline = registry.GetPipeline(configuredPipeline);
        }

        var services = new ServiceCollection();
        var optionsRegistry = setBuilderRegistry ? null : registry;
        var builder = addConsumers
            ? useOptionsFactory
                ? services.AddConsumers(_ => new ConsumersOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddConsumers(options => { options.ResiliencePipelineRegistry = optionsRegistry; })
            : useOptionsFactory
                ? services.AddBrighter(_ => new BrighterOptions { ResiliencePipelineRegistry = optionsRegistry })
                : services.AddBrighter(options => { options.ResiliencePipelineRegistry = optionsRegistry; });
        if (setBuilderRegistry)
            builder.ResiliencePolicyRegistry = registry;

        using var provider = services.BuildServiceProvider();

        // Act
        var processor = provider.GetRequiredService<IAmACommandProcessor>();

        // Assert
        Assert.NotNull(processor);
        Assert.Same(registry, provider.GetRequiredService<IBrighterOptions>().ResiliencePipelineRegistry);
        Assert.NotNull(registry.GetPipeline(CommandProcessor.OutboxProducer));
        Assert.NotNull(registry.GetPipeline(CommandProcessor.RequestReply));
        if (configuredPipeline is not null)
            Assert.Same(customPipeline, registry.GetPipeline(configuredPipeline));
    }
}
