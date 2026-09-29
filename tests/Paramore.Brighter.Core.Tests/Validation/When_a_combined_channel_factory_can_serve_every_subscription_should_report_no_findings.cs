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

public class CombinedChannelFactoryServesEveryConfiguredSubscriptionValidationTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_combined_channel_factory_can_serve_every_subscription_should_report_no_findings()
    {
        // Arrange — a combined channel factory whose inner factories match every subscription's
        // declared type
        var defaultChannelFactory = new CombinedChannelFactory([new DeclaredChannelFactory(), new NonMatchingChannelFactory()]);
        var subA = new DeclaringSubscription(subscriptionName: new SubscriptionName("sub-a"));
        var subB = new NonMatchingSubscription(subscriptionName: new SubscriptionName("sub-b"));

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act
        var satisfiedA = spec.IsSatisfiedBy(subA);
        var collectorA = new ValidationResultCollector<Subscription>();
        var resultsA = spec.Accept(collectorA).ToList();

        var satisfiedB = spec.IsSatisfiedBy(subB);
        var collectorB = new ValidationResultCollector<Subscription>();
        var resultsB = spec.Accept(collectorB).ToList();

        // Assert — no findings for either subscription
        await Assert.That(satisfiedA).IsTrue();
        await Assert.That(resultsA).IsEmpty();
        await Assert.That(satisfiedB).IsTrue();
        await Assert.That(resultsB).IsEmpty();
    }
}
