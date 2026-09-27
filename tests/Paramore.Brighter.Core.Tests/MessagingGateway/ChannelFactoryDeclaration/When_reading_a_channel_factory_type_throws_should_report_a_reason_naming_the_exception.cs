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

using System;
using System.Linq;
using Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration;

public class ChannelFactoryDeclarationSweepThrowingReadTests
{
    [Fact]
    public void When_reading_a_channel_factory_type_throws_should_report_a_reason_naming_the_exception()
    {
        // Arrange
        // Evident Data: a double whose ChannelFactoryType getter throws unconditionally - reading it
        // must not terminate the whole sweep, and must not silently drop the subject.
        var throwingType = typeof(ThrowingChannelFactoryTypeSubscription);
        var soundType = typeof(SoundBaseDeclaringSubscription);

        // Act
        var result = Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep(throwingType.Assembly);

        // Assert
        // The throwing type is reported with a non-null reason naming the type, the exception type
        // and its message - a fault, not a skip
        var throwingEntry = result.Single(entry => entry.Subject == throwingType);
        Assert.NotNull(throwingEntry.Reason);
        Assert.Contains(throwingType.FullName!, throwingEntry.Reason);
        Assert.Contains(typeof(InvalidOperationException).FullName!, throwingEntry.Reason);
        Assert.Contains("Deliberately unreadable ChannelFactoryType.", throwingEntry.Reason);

        // The rest of the sweep still completes - a sound subject elsewhere in the same assembly is
        // still reported, in the same run
        Assert.Contains(result, entry => entry.Subject == soundType && entry.Reason is null);
    }
}
