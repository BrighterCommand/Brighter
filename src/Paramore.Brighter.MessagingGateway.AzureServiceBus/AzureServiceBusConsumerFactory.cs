#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;
using IServiceBusClientProvider = Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider.IServiceBusClientProvider;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Factory class for creating instances of <see cref="AzureServiceBusConsumer"/>
/// </summary>
public class AzureServiceBusConsumerFactory : IAmAMessageConsumerFactory
{
    private readonly IServiceBusClientProvider _clientProvider;
    private readonly bool _ackOnRead;

    /// <summary>
    /// Factory to create an Azure Service Bus Consumer
    /// </summary>
    /// <param name="configuration">The configuration to connect to <see cref="AzureServiceBusConfiguration"/></param>
    public AzureServiceBusConsumerFactory(AzureServiceBusConfiguration configuration)
        : this(new ServiceBusConnectionStringClientProvider(configuration.ConnectionString), configuration.AckOnRead)
    { }

    /// <summary>
    /// Factory to create an Azure Service Bus Consumer
    /// </summary>
    /// <param name="clientProvider">A client Provider <see cref="IServiceBusClientProvider"/> to determine how to connect to ASB</param>
    /// <param name="ackOnRead">When set to True this will remove the message from the channel when it is read.</param>
    public AzureServiceBusConsumerFactory(IServiceBusClientProvider clientProvider, bool ackOnRead = false)
    {
        _clientProvider = clientProvider;
        _ackOnRead = ackOnRead;
    }

    /// <summary>
    /// Creates a consumer for the specified queue.
    /// </summary>
    /// <param name="subscription">The queue to connect to</param>
    /// <returns>IAmAMessageConsumerSync</returns>
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        var nameSpaceManagerWrapper = new AdministrationClientWrapper(_clientProvider);

        if (!(subscription is AzureServiceBusSubscription sub))
            throw new ArgumentException("Subscription is not of type AzureServiceBusSubscription.",
                nameof(subscription));

        var receiverProvider = new ServiceBusReceiverProvider(_clientProvider);

        if (sub.Configuration.UseServiceBusQueue)
        {
            var messageProducer = new AzureServiceBusQueueMessageProducer(
                nameSpaceManagerWrapper,
                new ServiceBusSenderProvider(_clientProvider),
                new AzureServiceBusPublication { MakeChannels = subscription.MakeChannels });

            return new AzureServiceBusQueueConsumer(
                sub,
                messageProducer,
                nameSpaceManagerWrapper,
                receiverProvider,
                _ackOnRead);
        }
        else
        {
            var messageProducer = new AzureServiceBusTopicMessageProducer(
                nameSpaceManagerWrapper,
                new ServiceBusSenderProvider(_clientProvider),
                new AzureServiceBusPublication { MakeChannels = subscription.MakeChannels });

            return new AzureServiceBusTopicConsumer(
                sub,
                messageProducer,
                nameSpaceManagerWrapper,
                receiverProvider);
        }
    }

    /// <summary>
    /// Creates a consumer for the specified queue.
    /// </summary>
    /// <param name="subscription">The queue to connect to</param>
    /// <returns>IAmAMessageConsumerSync</returns>
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        if (Create(subscription) is not IAmAMessageConsumerAsync consumer)
            throw new ChannelFailureException("AzureServiceBusConsumerFactory: Failed to create an async consumer");
        return consumer;
    }
}
