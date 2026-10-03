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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration;

public class ChannelFactoryDeclarationSweepUnsoundReasonsTests
{
    [Fact]
    public void When_sweeping_an_assembly_containing_unsound_declarations_should_report_their_reasons()
    {
        // Arrange
        // Evident Data: sweeping the whole Core.Tests assembly, located from one of its own subjects
        var subscriptionType = typeof(NonFactoryDeclaringSubscription);

        // Act
        var result = Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep(subscriptionType.Assembly);

        // Assert
        // The not-a-channel-factory double (task 38) carries the expected non-null reason
        var notAFactoryEntry = result.Single(e => e.Subject == typeof(NonFactoryDeclaringSubscription));
        Assert.NotNull(notAFactoryEntry.Reason);
        Assert.Contains(typeof(NonFactoryDeclaringSubscription).FullName!, notAFactoryEntry.Reason);
        Assert.Contains(typeof(NotAChannelFactory).FullName!, notAFactoryEntry.Reason);

        // The inherited-default double (task 39) carries the expected non-null reason
        var inheritedDefaultEntry = result.Single(e => e.Subject == typeof(DefaultChannelFactoryDeclaringSubscription));
        Assert.NotNull(inheritedDefaultEntry.Reason);
        Assert.Contains(typeof(DefaultChannelFactoryDeclaringSubscription).FullName!, inheritedDefaultEntry.Reason);
        Assert.Contains(typeof(InMemoryChannelFactory).FullName!, inheritedDefaultEntry.Reason);
    }
}
