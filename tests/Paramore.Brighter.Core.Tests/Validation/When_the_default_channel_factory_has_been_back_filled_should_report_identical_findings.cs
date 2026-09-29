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

public class ChannelFactoryCompatibleInvariantToBackFillValidationTests
{
    [Test]
    public async System.Threading.Tasks.Task When_the_default_channel_factory_has_been_back_filled_should_report_identical_findings()
    {
        // Arrange — the AC-4 configuration: sub-a's own ChannelFactory is null, the default
        // channel factory does not match its declared type
        var defaultChannelFactory = new NonMatchingChannelFactory();
        var subscription = new DeclaringSubscription(subscriptionName: new SubscriptionName("sub-a"));

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act — evaluate before DispatchBuilder.Subscriptions() would back-fill the default
        spec.IsSatisfiedBy(subscription);
        var beforeCollector = new ValidationResultCollector<Subscription>();
        var before = spec.Accept(beforeCollector).ToList();

        // Act — simulate DispatchBuilder.Subscriptions()'s back-fill: write the same default
        // instance into the subscription's own ChannelFactory, then evaluate again
        subscription.ChannelFactory = defaultChannelFactory;
        spec.IsSatisfiedBy(subscription);
        var afterCollector = new ValidationResultCollector<Subscription>();
        var after = spec.Accept(afterCollector).ToList();

        // Assert — byte-identical findings, before and after the back-fill
        await Assert.That(before).HasSingleItem();
        await Assert.That(after).HasSingleItem();
        await Assert.That(after[0].Error!.Source).IsEqualTo(before[0].Error!.Source);
        await Assert.That(after[0].Error!.Message).IsEqualTo(before[0].Error!.Message);
    }
}
