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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ChannelFactoryMismatchThrowOnErrorFalseTests
{
    [Fact]
    public async Task When_a_channel_factory_mismatch_is_validated_without_throw_on_error_should_start_and_report()
    {
        // Arrange — the AC-16 configuration, but with throwOnError: false
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);

        var services = new ServiceCollection();
        services.AddConsumers(options =>
            {
                options.DefaultChannelFactory = new ExtensionsNonMatchingChannelFactory();
                options.Subscriptions =
                [
                    new ExtensionsDeclaringSubscription(subscriptionName: new SubscriptionName("sub-a"))
                ];
            })
            .ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>();
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act — the host starts successfully; a mismatch under throwOnError: false must not throw
        await service.StartAsync(CancellationToken.None);

        // Assert — the host started and reached Receive
        Assert.True(dispatcher.ReceiveWasCalled);

        // Assert — the mismatch Error is still present in the validation results
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();
        Assert.Contains(
            result.Errors,
            e => e.Severity == ValidationSeverity.Error
                && e.Source == "Subscription 'sub-a'"
                && e.Message.Contains(nameof(ExtensionsDeclaredChannelFactory)));
    }
}
