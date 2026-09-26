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

public class ChannelFactoryDeclarationSweepUnsatisfiableGenericConstraintTests
{
    [Fact]
    public void When_a_generic_subscription_cannot_be_closed_should_report_a_reason_rather_than_skip()
    {
        // Arrange
        // Evident Data: an open generic subscription constrained `where T : IEvent`, which the
        // representative closing argument, Command, does not satisfy (Command implements ICommand,
        // not IEvent) - MakeGenericType raises a constraint-violation ArgumentException.
        var openDefinition = typeof(SoundGenericConstrainedToEventDeclaringSubscription<>);

        // Act
        var result = Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep(openDefinition.Assembly);

        // Assert
        // The subject is present - not silently skipped - and carries a non-null reason
        var entry = result.Single(e => e.Subject == openDefinition);
        Assert.NotNull(entry.Reason);

        // The reason identifies the closing failure specifically: the representative argument that
        // could not satisfy the constraint, and the ArgumentException MakeGenericType raised for it -
        // distinct from a getter-throws reason (task 47), which never names the closing argument
        Assert.Contains(typeof(Command).FullName!, entry.Reason);
        Assert.Contains(typeof(ArgumentException).FullName!, entry.Reason);
    }
}
