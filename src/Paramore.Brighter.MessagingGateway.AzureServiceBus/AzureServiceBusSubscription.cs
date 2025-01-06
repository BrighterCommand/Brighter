#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// A <see cref="Subscription"/> with Specific option for Azure Service Bus.
/// </summary>
public class AzureServiceBusSubscription : Subscription
{
    public AzureServiceBusSubscriptionConfiguration Configuration { get; }

    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
    /// </summary>
    /// <param name="dataType">The type for this Subscription.</param>
    /// <param name="name">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="bufferSize">The number of messages to buffer on the channel</param>
    /// <param name="noOfPerformers">The no of performers.</param>
    /// <param name="timeOut">The timeout to wait. Defaults to 300ms</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The number of milliseconds to delay the delivery of a requeue message for.</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType"></param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    public AzureServiceBusSubscription(
        Type dataType,
        SubscriptionName? name = null,
        ChannelName? channelName = null,
        RoutingKey? routingKey = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Unknown,
        IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        AzureServiceBusSubscriptionConfiguration? subscriptionConfiguration = null,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null)
        : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeOut, requeueCount, 
            requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, 
            channelFailureDelay)
    {
        Configuration = subscriptionConfiguration ?? new AzureServiceBusSubscriptionConfiguration();
    }
}

/// <summary>
/// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
/// </summary>
/// <typeparam name="T">The type of Subscription.</typeparam>
public class AzureServiceBusSubscription<T> : AzureServiceBusSubscription where T : IRequest
{
    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusSubscription"/>
    /// </summary>
    /// <param name="name">The name. Defaults to the data type's full name.</param>
    /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
    /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
    /// <param name="bufferSize">The number of messages to buffer on the channel</param>
    /// <param name="noOfPerformers">The no of performers.</param>
    /// <param name="timeOut">The timeout to wait for messages; defaults to 300ms</param>
    /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
    /// <param name="requeueDelay">The delay the delivery of a requeue message. 0 is no delay. Defaults to 0</param>
    /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
    /// <param name="messagePumpType"></param>
    /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
    /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
    /// <param name="subscriptionConfiguration">The configuration options for the subscriptions.</param>
    /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
    /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
    public AzureServiceBusSubscription(
        SubscriptionName? name = null,
        ChannelName? channelName = null,
        RoutingKey? routingKey = null,
        int bufferSize = 1,
        int noOfPerformers = 1,
        TimeSpan? timeOut = null,
        int requeueCount = -1,
        TimeSpan? requeueDelay = null,
        int unacceptableMessageLimit = 0,
        MessagePumpType messagePumpType = MessagePumpType.Proactor,
        IAmAChannelFactory? channelFactory = null,
        OnMissingChannel makeChannels = OnMissingChannel.Create,
        AzureServiceBusSubscriptionConfiguration? subscriptionConfiguration = null,
        TimeSpan? emptyChannelDelay = null,
        TimeSpan? channelFailureDelay = null)
        : base(typeof(T), name, channelName, routingKey, bufferSize, noOfPerformers,
            timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, 
            subscriptionConfiguration, emptyChannelDelay, channelFailureDelay)
    {
    }
}
