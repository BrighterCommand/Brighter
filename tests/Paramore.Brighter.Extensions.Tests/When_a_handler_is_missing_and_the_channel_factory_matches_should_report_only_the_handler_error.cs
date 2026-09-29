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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;


namespace Paramore.Brighter.Extensions.Tests;

public class HandlerMissingWithMatchedChannelFactoryTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_handler_is_missing_and_the_channel_factory_matches_should_report_only_the_handler_error()
    {
        // Arrange — a subscription whose RequestType has no registered handler and whose channel
        // factory is correctly matched (its own ChannelFactory is exactly the type it declares)
        var services = new ServiceCollection();
        services.AddConsumers(options =>
            {
                options.Subscriptions =
                [
                    new ExtensionsDeclaringSubscription(
                        subscriptionName: new SubscriptionName("sub-a"),
                        channelFactory: new ExtensionsDeclaredChannelFactory())
                ];
            })
            .ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act — every registered validator's results are combined, matching how
        // BrighterValidationHostedService itself resolves them (ADR 0074 registers a second
        // IAmAPipelineValidator alongside the core one, so a single GetRequiredService call would
        // resolve only the last-registered validator, not the union of both)
        var validators = provider.GetServices<IAmAPipelineValidator>();
        var result = PipelineValidationResult.Combine(validators.Select(v => v.Validate()).ToArray());

        // Assert — exactly one Error, from the pre-existing HandlerRegistered rule; the
        // channel-factory compatibility rule contributes no additional finding
        var error = await Assert.That(result.Errors).HasSingleItem();
        await Assert.That(error.Source).IsEqualTo("Subscription 'sub-a'");
        await Assert.That(error.Message).Contains("No handler registered");
    }
}
