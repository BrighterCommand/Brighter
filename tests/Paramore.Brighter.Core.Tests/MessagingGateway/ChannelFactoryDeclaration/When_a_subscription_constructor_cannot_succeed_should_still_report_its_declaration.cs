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
using Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration.TestDoubles;

namespace Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration;

public class ChannelFactoryDeclarationSweepConstructorFailureTests
{
    [Test]
    public async System.Threading.Tasks.Task When_a_subscription_constructor_cannot_succeed_should_still_report_its_declaration()
    {
        // Arrange
        // Evident Data: the double's own constructor throws ConfigurationException if invoked
        // (MessagePumpType.Unknown, Subscription.cs:213), but declares the sound SoundChannelFactory
        var subscriptionType = typeof(SoundChannelFactoryDeclaringSubscription);

        // Act
        var result = Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep(subscriptionType.Assembly);

        // Assert
        // It appears among its assembly's subjects with a null Reason - which it can only do
        // because no constructor ran; a real construction attempt would throw ConfigurationException
        var entry = result.Single(e => e.Subject == subscriptionType);
        await Assert.That(entry.Reason).IsNull();
    }
}
