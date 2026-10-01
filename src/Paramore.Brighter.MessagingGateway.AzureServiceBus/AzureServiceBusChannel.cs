#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Diagnostics;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

internal sealed class AzureServiceBusChannel(Subscription subscription, AzureServiceBusConsumer consumer)
    : Channel(subscription.ChannelName, subscription.RoutingKey, consumer, subscription.BufferSize)
{
    public override Message Receive(TimeSpan? timeout = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(1);
        var elapsed = Stopwatch.StartNew();
        do
        {
            var remaining = wait - elapsed.Elapsed;
            var message = base.Receive(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
            if (BrighterAsyncContext.Run(() => consumer.CanDispatchAsync(message))) return message;
        } while (elapsed.Elapsed < wait);

        return new Message();
    }
}
