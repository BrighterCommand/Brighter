using System;
using Paramore.Brighter;
using Polly.Retry;

namespace Polly.Registry;

public static class ResiliencePipelineRegistryExtensions
{
    public static ResiliencePipelineRegistry<string> AddBrighterDefault(this ResiliencePipelineRegistry<string> registry)
    {
        registry
            .TryAddBuilder(CommandProcessor.OutboxProducer,
            (builder, _) => builder
                .AddRetry(new RetryStrategyOptions
                {
                    Delay = TimeSpan.FromMilliseconds(50),
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Linear
                }));
        
        registry
            .TryAddBuilder(CommandProcessor.RequestReply,
                (builder, _) => builder
                    .AddRetry(new RetryStrategyOptions
                    {
                        Delay = TimeSpan.FromMilliseconds(50),
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Linear
                    }));
        return registry;
    }
}
