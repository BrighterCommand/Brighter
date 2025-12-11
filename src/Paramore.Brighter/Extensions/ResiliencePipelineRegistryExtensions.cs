#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter;
using Polly.Retry;

namespace Polly.Registry;

public static class ResiliencePipelineRegistryExtensions
{
    /// <summary>
    /// Adds default strategies to the <see cref="ResiliencePipelineRegistry{TKey}"/> needed by Brighter internally to
    /// ensure the resilience of calls, such as to a <see cref="IAmAMessageProducer"/>
    ///
    /// We add:
    ///
    ///  CommandProcessor.OutboxProducer => new RetryStrategyOptions
    /// {
    ///    Delay = TimeSpan.FromMilliseconds(50),
    ///    MaxRetryAttempts = 3,
    ///    BackoffType = DelayBackoffType.Linear
    /// })
    ///
    /// CommandProcessor.RequestReply => new RetryStrategyOptions
    /// {
    ///    Delay = TimeSpan.FromMilliseconds(50),
    ///    MaxRetryAttempts = 3,
    ///    BackoffType = DelayBackoffType.Linear
    /// }) 
    /// 
    /// </summary>
    /// <param name="registry">The resilience pipeline registry</param>
    /// <returns></returns>
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
