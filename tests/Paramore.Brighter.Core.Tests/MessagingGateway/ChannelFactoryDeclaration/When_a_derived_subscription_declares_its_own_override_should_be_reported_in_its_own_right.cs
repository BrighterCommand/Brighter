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

public class ChannelFactoryDeclarationSweepDeclaringDerivedSubsumptionTests
{
    [Fact]
    public void When_a_derived_subscription_declares_its_own_override_should_be_reported_in_its_own_right()
    {
        // Arrange
        // Evident Data: a base/derived pair where the derived type declares its own
        // ChannelFactoryType override rather than inheriting the base's - so subsumption's
        // "declares no override" conjunct is false for it, and it must be reported alongside
        // its base rather than dropped.
        var baseType = typeof(SoundBaseDeclaringSubscription);
        var derivedType = typeof(OverridingDerivedDeclaringSubscription);

        // Act
        var result = Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep(derivedType.Assembly);

        // Assert
        // The derived type declares its own override, so it is reported in its own right
        Assert.Contains(result, entry => entry.Subject == derivedType);

        // The base is reported too - both types stand, neither subsumes the other here
        Assert.Contains(result, entry => entry.Subject == baseType);
    }
}
