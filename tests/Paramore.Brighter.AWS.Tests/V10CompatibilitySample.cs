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

using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.AWS.Tests.TestDoubles;

namespace Paramore.Brighter.AWS.Tests;

/// <summary>
/// Compile-only V10 compatibility sample for <see cref="SqsSubscription{T}"/>.
/// Constructs the type with the argument shapes a V10 application uses today.
/// This file is intentionally not a test: it asserts nothing and requires no broker infrastructure.
/// </summary>
internal static class V10CompatibilitySample
{
    internal static void BuildSamples()
    {
        // SqsSubscription<T> — named-argument style, including requeueCount
        _ = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("my-sub"),
            channelName: new ChannelName("my-channel"),
            routingKey: new RoutingKey("my-topic"),
            requeueCount: 3);

        // SqsSubscription<T> — positional-argument style with explicit channel type
        _ = new SqsSubscription<MyCommand>(
            new SubscriptionName("my-sub-2"),
            new ChannelName("my-channel-2"),
            ChannelType.PubSub,
            new RoutingKey("my-topic-2"),
            requeueCount: 3);
    }
}
