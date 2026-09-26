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

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy;

public class CommandProcessorPumpActionTests
{
    [Fact]
    public async Task When_sending_a_command_with_a_pump_action_should_not_retry()
    {
        //Arrange
        var subscribers = new SubscriberRegistry();
        subscribers.RegisterAsync<ResilienceActionCommandAsync, ResilienceActionHandlerAsync>();
        var services = new ServiceCollection();
        services.AddTransient<ResilienceActionHandlerAsync>();
        services.AddTransient<ResilienceExceptionPolicyHandlerAsync<ResilienceActionCommandAsync>>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        await using var provider = services.BuildServiceProvider();
        using var pipelines = new ResiliencePipelineRegistry<string>();
        pipelines.TryAddBuilder("pump-actions", (builder, _) => builder.AddRetry(
            new RetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.Zero }));
        var processor = new CommandProcessor(subscribers, new ServiceProviderHandlerFactory(provider),
            new InMemoryRequestContextFactory(), new PolicyRegistry(), pipelines, new InMemorySchedulerFactory());
        var action = new RejectMessageAction("permanent failure");
        var command = new ResilienceActionCommandAsync(action);

        //Act
        var thrown = await Assert.ThrowsAsync<RejectMessageAction>(() => processor.SendAsync(command));

        //Assert
        Assert.Same(action, thrown);
        Assert.Equal(1, command.Attempts);
    }
}
