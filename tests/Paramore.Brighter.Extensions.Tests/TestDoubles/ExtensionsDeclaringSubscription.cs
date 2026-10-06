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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// This project's own copy (C-9) of the channel-factory compatibility rule's subscription double.
/// Declares <see cref="ExtensionsDeclaredChannelFactory"/> as its <see cref="ChannelFactoryType"/>.
/// Identity-only, in the same sense as the channel factory doubles: it contributes nothing beyond
/// that declaration.
/// </summary>
public class ExtensionsDeclaringSubscription : Subscription
{
    public ExtensionsDeclaringSubscription(
        SubscriptionName? subscriptionName = null,
        IAmAChannelFactory? channelFactory = null,
        Func<Message, Type>? getRequestType = null,
        MessagePumpType messagePumpType = MessagePumpType.Proactor)
        : base(
            subscriptionName ?? new SubscriptionName(typeof(ExtensionsFakeChannelFactoryRequest).FullName!),
            new ChannelName(typeof(ExtensionsFakeChannelFactoryRequest).FullName!),
            new RoutingKey(typeof(ExtensionsFakeChannelFactoryRequest).FullName!),
            requestType: getRequestType is null ? typeof(ExtensionsFakeChannelFactoryRequest) : null,
            getRequestType: getRequestType,
            messagePumpType: messagePumpType,
            channelFactory: channelFactory)
    {
    }

    public override Type ChannelFactoryType => typeof(ExtensionsDeclaredChannelFactory);
}
