#region Licence
/* The MIT License (MIT)
Copyright © 2026 Anurag Mishra <am6840957@gmail.com>

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

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

/// <summary>
/// A client that returns a supplied receiver and records the options the transport asked for, so a test can
/// assert which receive settings reached the broker.
/// </summary>
public class RecordingReceiverOptionsServiceBusClient(ServiceBusReceiver receiver) : ServiceBusClient, IServiceBusClientProvider
{
    /// <summary>The options passed to <see cref="CreateReceiver(string, ServiceBusReceiverOptions)"/> or its topic overload.</summary>
    public ServiceBusReceiverOptions? ReceiverOptions { get; private set; }

    /// <summary>The options passed to <see cref="AcceptNextSessionAsync(string, ServiceBusSessionReceiverOptions?, CancellationToken)"/> or its topic overload.</summary>
    public ServiceBusSessionReceiverOptions? SessionReceiverOptions { get; private set; }

    public override ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options)
    {
        ReceiverOptions = options;
        return receiver;
    }

    public override ServiceBusReceiver CreateReceiver(string topicName, string subscriptionName,
        ServiceBusReceiverOptions options)
    {
        ReceiverOptions = options;
        return receiver;
    }

    public override Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(string queueName,
        ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        SessionReceiverOptions = options;
        return Task.FromResult((ServiceBusSessionReceiver)receiver);
    }

    public override Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(string topicName, string subscriptionName,
        ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        SessionReceiverOptions = options;
        return Task.FromResult((ServiceBusSessionReceiver)receiver);
    }

    public ServiceBusClient GetServiceBusClient() => this;

    public ServiceBusAdministrationClient GetServiceBusAdministrationClient()
        => new InMemoryServiceBusAdministrationClient();

    public override async ValueTask DisposeAsync() => await receiver.CloseAsync();
}
