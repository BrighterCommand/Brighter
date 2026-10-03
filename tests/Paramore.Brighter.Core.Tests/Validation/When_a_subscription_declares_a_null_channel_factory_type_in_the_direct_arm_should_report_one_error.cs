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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class NullDeclaredChannelFactoryTypeDirectArmValidationTests
{
    [Fact]
    public void When_a_subscription_declares_a_null_channel_factory_type_in_the_direct_arm_should_report_one_error()
    {
        // Arrange — a subscription whose ChannelFactoryType is overridden to null, in a direct-arm
        // (non-combined) configuration
        var subscription = new NullDeclaringSubscription(subscriptionName: new SubscriptionName("null-sub"));
        var defaultChannelFactory = new DeclaredChannelFactory();

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Error for null-sub
        Assert.False(satisfied);
        var result = Assert.Single(results);
        Assert.Equal("Subscription 'null-sub'", result.Error!.Source);

        var message = result.Error.Message;
        Assert.Contains("no ChannelFactoryType", message);
        Assert.EndsWith(
            $"— use a subscription type whose ChannelFactoryType is {typeof(DeclaredChannelFactory).FullName}",
            message);
        Assert.DoesNotContain("configure a channel factory of type", message);
    }
}
