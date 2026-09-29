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


namespace Paramore.Brighter.Extensions.Tests;

public class ChannelFactoryMismatchThrowOnErrorTrueTests
{
    [Test]
    public async Task When_a_channel_factory_mismatch_is_validated_with_throw_on_error_true_should_fail_startup()
    {
        // Arrange — a host configured with AddConsumers containing one subscription that declares
        // ExtensionsDeclaredChannelFactory but falls back to a mismatched default channel factory
        // (the AC-1 shape, using this project's own doubles), validated with throwOnError: true
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
            .ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>();
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;

        // a spy dispatcher stands in for the real one, so this exercises real AddConsumers/
        // ValidatePipelines wiring without needing a fully built message pump
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act & Assert — startup fails and Receive is never reached
        var exception = await Assert.That(() => service.StartAsync(CancellationToken.None)).ThrowsExactly<PipelineValidationException>();
        await Assert.That(dispatcher.ReceiveWasCalled).IsFalse();

        // Assert — the reported findings include the mismatch Error, with the expected Source and message
        // (a second, unrelated "no handler registered" Error is also expected here — this fixture's
        // local request type has no handler registered — so this checks inclusion, not exclusivity)
        await Assert.That(exception.ValidationResult).IsNotNull();
        await Assert.That(exception.ValidationResult!.Errors).Contains(e => e.Severity == ValidationSeverity.Error
                && e.Source == "Subscription 'sub-a'"
                && e.Message.Contains(nameof(ExtensionsDeclaredChannelFactory)));
    }
}
