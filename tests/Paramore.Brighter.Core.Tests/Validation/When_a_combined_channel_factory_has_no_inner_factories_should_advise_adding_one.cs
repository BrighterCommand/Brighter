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

namespace Paramore.Brighter.Core.Tests.Validation;

public class EmptyCombinedChannelFactoryAdvisesAddingOneValidationTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_combined_channel_factory_has_no_inner_factories_should_advise_adding_one()
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
        await Assert.That(satisfied).IsFalse();
        var result = await Assert.That(results).HasSingleItem();
        await Assert.That(result.Error!.Source).IsEqualTo("Subscription 'empty-sub'");

        var message = result.Error.Message;
        await Assert.That(message).EndsWith("— add a channel factory to the combined channel factory");
        await Assert.That(message).DoesNotContain("is one of:");
    }
}
