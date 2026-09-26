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

namespace Paramore.Brighter.Core.Tests.MessagingGateway.ChannelFactoryDeclaration.TestDoubles;

/// <summary>
/// A subscription double whose parameterless constructor cannot succeed - it passes
/// <see cref="MessagePumpType.Unknown"/> to the base constructor, which rejects it - but which
/// declares the sound <see cref="SoundChannelFactory"/> as its <see cref="ChannelFactoryType"/>.
/// Exists to be read by <see cref="Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep"/>
/// without ever being constructed.
/// </summary>
public class SoundChannelFactoryDeclaringSubscription : Subscription
{
    public SoundChannelFactoryDeclaringSubscription()
        : base(
            new SubscriptionName(typeof(ChannelFactoryDeclarationRequest).FullName!),
            new ChannelName(typeof(ChannelFactoryDeclarationRequest).FullName!),
            new RoutingKey(typeof(ChannelFactoryDeclarationRequest).FullName!),
            requestType: typeof(ChannelFactoryDeclarationRequest),
            messagePumpType: MessagePumpType.Unknown)
    {
    }

    public override Type ChannelFactoryType => typeof(SoundChannelFactory);
}
