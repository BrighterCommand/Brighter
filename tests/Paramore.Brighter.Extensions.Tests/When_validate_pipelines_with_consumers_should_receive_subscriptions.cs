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

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ValidatePipelinesWithConsumersTests
{
    [Fact]
    public void When_validate_pipelines_with_consumers_should_detect_missing_handler()
    {
        // Arrange — set up a subscription for a request type with no handler registered
        var services = new ServiceCollection();
        services.AddConsumers(options =>
        {
            options.Subscriptions =
            [
                new Subscription<UnhandledEvent>(
                    new SubscriptionName("unhandled-sub"),
                    new ChannelName("unhandled-channel"),
                    new RoutingKey("unhandled.event"),
                    messagePumpType: MessagePumpType.Proactor)
            ];
        })
        .ValidatePipelines();

        var provider = services.BuildServiceProvider();

        // Act — resolve validator and validate
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — should detect the subscription has no handler registered
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("No handler registered"));
    }

    [Fact]
    public void When_add_consumers_should_register_consumer_validation_specs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddConsumers();

        var provider = services.BuildServiceProvider();

        // Act — resolve consumer validation specs
        var specs = provider.GetServices<ISpecification<Subscription>>().ToList();

        // Assert — AddConsumers should register 3 consumer validation specs
        Assert.Equal(3, specs.Count);
    }

    private class UnhandledEvent : Event
    {
        public UnhandledEvent() : base(Guid.NewGuid()) { }
    }
}
