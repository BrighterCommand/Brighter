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
/// An open generic <see cref="Subscription"/> subclass that declares its own sound
/// <see cref="SoundChannelFactory"/> override. The only generic <see cref="Subscription"/> subclass
/// in this assembly, so it is the sole cover for
/// <see cref="Paramore.Brighter.SubscriptionChannelFactoryDeclaration.Sweep"/>'s generic-closing step:
/// the open definition <c>typeof(SoundGenericDeclaringSubscription&lt;&gt;)</c> is the candidate the
/// sweep discovers and reports, while a closed construction against a representative argument is
/// what it actually instantiates and reads.
/// </summary>
public class SoundGenericDeclaringSubscription<T> : Subscription
    where T : IRequest
{
    public SoundGenericDeclaringSubscription()
        : base(
            new SubscriptionName(typeof(T).FullName!),
            new ChannelName(typeof(T).FullName!),
            new RoutingKey(typeof(T).FullName!),
            requestType: typeof(T),
            messagePumpType: MessagePumpType.Unknown)
    {
    }

    public override Type ChannelFactoryType => typeof(SoundChannelFactory);
}
