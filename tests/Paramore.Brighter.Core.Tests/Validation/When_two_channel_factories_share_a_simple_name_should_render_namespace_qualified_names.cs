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
using System.Text.RegularExpressions;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.ServiceActivator.Validation;
using Xunit;
using AlphaSubscription = Paramore.Brighter.Core.Tests.Validation.TestDoubles.AlphaBus.AlphaSubscription;
using AlphaChannelFactory = Paramore.Brighter.Core.Tests.Validation.TestDoubles.AlphaBus.ChannelFactory;
using BetaChannelFactory = Paramore.Brighter.Core.Tests.Validation.TestDoubles.BetaBus.ChannelFactory;

namespace Paramore.Brighter.Core.Tests.Validation;

public class TwoChannelFactoriesSharingASimpleNameRenderNamespaceQualifiedNamesTests
{
    [Fact]
    public void When_two_channel_factories_share_a_simple_name_should_render_namespace_qualified_names()
    {
        // Arrange — an AlphaSubscription (declares AlphaBus.ChannelFactory) handed a BetaBus.ChannelFactory:
        // two distinct types both simply named "ChannelFactory"
        var subscription = new AlphaSubscription(
            subscriptionName: new SubscriptionName("alpha-sub"),
            channelFactory: new BetaChannelFactory());

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory: null);

        // Act
        spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — both namespace-qualified display names appear in full, and no bare "ChannelFactory"
        // token (one not preceded by '.' and not part of "ChannelFactoryType") appears anywhere
        var result = Assert.Single(results);
        var message = result.Error!.Message;
        Assert.Contains(
            "Paramore.Brighter.Core.Tests.Validation.TestDoubles.AlphaBus.ChannelFactory",
            message);
        Assert.Contains(
            "Paramore.Brighter.Core.Tests.Validation.TestDoubles.BetaBus.ChannelFactory",
            message);
        Assert.Empty(Regex.Matches(message, @"(?<![.\w])ChannelFactory(?!Type)"));
    }
}
