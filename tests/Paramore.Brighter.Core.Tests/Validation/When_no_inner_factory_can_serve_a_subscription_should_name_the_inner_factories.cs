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

public class NoInnerFactoryCanServeSubscriptionValidationTests
{
    [Fact]
    public void When_no_inner_factory_can_serve_a_subscription_should_name_the_inner_factories()
    {
        // Arrange — the AC-6 combined channel factory, but a plain subscription no inner factory matches
        var defaultChannelFactory = new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()]);
        var subscription = new Subscription<FakeChannelFactoryRequest>(subscriptionName: new SubscriptionName("greeting-sub"));

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Error naming both inner factories, in constructor order, and never
        // naming the composite itself
        Assert.False(satisfied);
        var result = Assert.Single(results);
        var message = result.Error!.Message;
        var declaredIndex = message.IndexOf(typeof(DeclaredChannelFactory).FullName!, System.StringComparison.Ordinal);
        var nonMatchingIndex = message.IndexOf(typeof(NonMatchingChannelFactory).FullName!, System.StringComparison.Ordinal);
        Assert.True(declaredIndex >= 0);
        Assert.True(nonMatchingIndex >= 0);
        Assert.True(declaredIndex < nonMatchingIndex);
        Assert.DoesNotContain(typeof(CombinedChannelFactory).FullName!, message);
        Assert.Contains("will be handed one of '", message);
    }
}
