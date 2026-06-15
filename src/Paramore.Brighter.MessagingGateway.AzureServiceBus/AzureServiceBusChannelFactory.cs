using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Tasks;

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

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Creates instances of <see cref="IAmAChannelSync"/>channels using Azure Service Bus.
/// </summary>
public class AzureServiceBusChannelFactory : IAmAChannelFactory
{
    private readonly AzureServiceBusConsumerFactory _azureServiceBusConsumerFactory;

    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusConsumerFactory"/>
    /// </summary>
    /// <param name="azureServiceBusConsumerFactory">An Azure Service Bus Consumer Factory.</param>
    public AzureServiceBusChannelFactory(AzureServiceBusConsumerFactory azureServiceBusConsumerFactory)
    {
        _azureServiceBusConsumerFactory = azureServiceBusConsumerFactory;
    }

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <param name="subscription">The parameters with which to create the channel for the transport</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is incorrect</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        var azureServiceBusSubscription = GetAndCheckSubscription(subscription);

        IAmAMessageConsumerSync messageConsumer =
            _azureServiceBusConsumerFactory.Create(azureServiceBusSubscription);

        return new Channel(
            channelName: subscription.ChannelName,
            routingKey: subscription.RoutingKey,
            messageConsumer: messageConsumer,
            maxQueueLength: subscription.BufferSize
        );
    }

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <param name="subscription">The parameters with which to create the channel for the transport</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is incorrect</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        var azureServiceBusSubscription = GetAndCheckSubscription(subscription);

        IAmAMessageConsumerAsync messageConsumer =
            _azureServiceBusConsumerFactory.CreateAsync(azureServiceBusSubscription);

        return new ChannelAsync(
            channelName: subscription.ChannelName,
            routingKey: subscription.RoutingKey,
            messageConsumer: messageConsumer,
            maxQueueLength: subscription.BufferSize
        );
    }

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <param name="ct">Cancel the ongoing operation</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is incorrect</exception>
    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken ct = default)
        => Task.FromResult(CreateAsyncChannel(subscription));
    
    private AzureServiceBusSubscription GetAndCheckSubscription(Subscription subscription)
    {
        if (subscription is not AzureServiceBusSubscription azureServiceBusSubscription)
        {
            throw new ConfigurationException(
                "We expect an AzureServiceBusSubscription or AzureServiceBusSubscription<T> as a parameter");
        }

        if (subscription.TimeOut < TimeSpan.FromMilliseconds(400))
        {
            throw new ArgumentException("The minimum allowed timeout is 400 milliseconds");
        }
        return azureServiceBusSubscription;
    }
}
