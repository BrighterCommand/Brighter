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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.ServiceActivator.Validation;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ProactorSubscriptionWithSyncHandlerValidationTests
{
    [Fact]
    public void When_proactor_subscription_has_sync_handler_should_report_error()
    {
        // Arrange — Proactor subscription with a sync handler registered
        var subscription = new Subscription(
            subscriptionName: new SubscriptionName("test-sub"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("test.routing.key"),
            requestType: typeof(MyDescribableCommand),
            messagePumpType: MessagePumpType.Proactor
        );

        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyDescribableCommand), typeof(MyPublicSyncHandler));

        var spec = ConsumerValidationRules.PumpHandlerMatch(registry);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Error!.Severity);
        Assert.Contains("Proactor", results[0].Error!.Message);
        Assert.Contains("Reactor", results[0].Error!.Message);
    }
}
