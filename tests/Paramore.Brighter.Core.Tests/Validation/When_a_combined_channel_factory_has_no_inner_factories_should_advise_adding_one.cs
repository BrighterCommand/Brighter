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

public class EmptyCombinedChannelFactoryAdvisesAddingOneValidationTests
{
    [Fact]
    public void When_a_combined_channel_factory_has_no_inner_factories_should_advise_adding_one()
    {
        // Arrange — a combined channel factory with no inner factories at all
        var defaultChannelFactory = new CombinedChannelFactory([]);
        var subscription = new DeclaringSubscription(subscriptionName: new SubscriptionName("empty-sub"));

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — exactly one Error for empty-sub
        Assert.False(satisfied);
        var result = Assert.Single(results);
        Assert.Equal("Subscription 'empty-sub'", result.Error!.Source);

        var message = result.Error.Message;
        Assert.EndsWith("— add a channel factory to the combined channel factory", message);
        Assert.DoesNotContain("is one of:", message);
    }
}
