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

public class PipelineValidationDisabledTests
{
    [Test]
    public async Task When_pipeline_validation_is_disabled_should_evaluate_no_rules()
    {
        // Arrange — the AC-16 mismatched configuration, but with pipeline validation disabled.
        // throwOnError: false so that, under the RED mutation, the validator runs and the test
        // fails on its no-results assertion rather than at host start.
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
            .ValidatePipelines(enabled: false, throwOnError: false);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>();
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act — the host starts successfully
        await service.StartAsync(CancellationToken.None);

        // Assert — the host started and reached Receive
        await Assert.That(dispatcher.ReceiveWasCalled).IsTrue();

        // Assert — no validation results are produced by any rule: the validator itself was
        // never registered (validation disabled), so there is nothing to evaluate
        await Assert.That(provider.GetService<IAmAPipelineValidator>()).IsNull();
    }
}
