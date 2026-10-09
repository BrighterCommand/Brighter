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

using Azure.Messaging.ServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.MessageScheduler.Azure;

internal sealed class AzureServiceBusRequeueScheduler(
    ServiceBusSender sender,
    RoutingKey schedulerTopic,
    TimeProvider timeProvider,
    IServiceBusClientProvider clientProvider,
    ServiceBusSenderOptions? senderOptions)
    : AzureServiceBusScheduler(sender, schedulerTopic, timeProvider),
        IAmAMessageRequeueSchedulerAsync, IAmAMessageRequeueSchedulerSync
{
    private readonly MessagingGateway.AzureServiceBus.AzureServiceBusRequeueScheduler _requeueScheduler =
        new(clientProvider, timeProvider, senderOptions);

    public Task RequeueAsync(Message message, ChannelName destination, TimeSpan delay,
        CancellationToken cancellationToken = default)
        => _requeueScheduler.RequeueAsync(message, destination, delay, cancellationToken);

    public void Requeue(Message message, ChannelName destination, TimeSpan delay)
        => _requeueScheduler.Requeue(message, destination, delay);
}
