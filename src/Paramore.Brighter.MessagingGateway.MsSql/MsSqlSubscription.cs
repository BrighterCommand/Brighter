#region Licence
/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlSubscription : Subscription
    {
        /// <inheritdoc />
        public override Type ChannelFactoryType => typeof(ChannelFactory);

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="requestType">Type of the data.</param>
        /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <paramref name="requestType"/> if null</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The delay the delivery of a requeue message. 0 is no delay. Defaults to 0</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public MsSqlSubscription(SubscriptionName subscriptionName,
            ChannelName channelName,
            RoutingKey routingKey,
            Type? requestType = null,
            Func<Message, Type>? getRequestType = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            TimeSpan? timeOut = null,
            int requeueCount = -1,
            TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0,
            MessagePumpType messagePumpType = MessagePumpType.Proactor,
            IAmAChannelFactory? channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null)
            : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize,
                noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit,
                messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
        { }
    }

    public class MsSqlSubscription<T> : MsSqlSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="getRequestType">The <see cref="Func{Message, Type}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout to wait for messages</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The Delay to the requeue of a message. 0 is no delay. Defaults to 0</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public MsSqlSubscription(SubscriptionName? subscriptionName = null,
            ChannelName? channelName = null,
            RoutingKey? routingKey = null,
            Func<Message, Type>? getRequestType = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            TimeSpan? timeOut = null,
            int requeueCount = -1,
            TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0,
            MessagePumpType messagePumpType = MessagePumpType.Proactor,
            IAmAChannelFactory? channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null)
            : base(
                subscriptionName ?? new SubscriptionName(typeof(T).FullName!), 
                channelName ?? new ChannelName(typeof(T).FullName!), 
                routingKey ?? new RoutingKey(typeof(T).FullName!), 
                typeof(T), 
                getRequestType, 
                bufferSize, 
                noOfPerformers,
                timeOut, 
                requeueCount, 
                requeueDelay, 
                unacceptableMessageLimit, 
                messagePumpType, 
                channelFactory, 
                makeChannels, 
                emptyChannelDelay, 
                channelFailureDelay)
        {
        }
       
    }
}
