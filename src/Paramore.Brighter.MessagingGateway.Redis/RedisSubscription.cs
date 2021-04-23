﻿#region Licence
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

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisSubscription : Subscription
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="dataType">Type of the data.</param>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public RedisSubscription(
            Type dataType, 
            SubscriptionName name = null, 
            ChannelName channelName = null, 
            RoutingKey routingKey = null, 
            int bufferSize = 1, 
            int noOfPerformers = 1, 
            int timeoutInMilliseconds = 300, 
            int requeueCount = -1, 
            int requeueDelayInMilliseconds = 0, 
            int unacceptableMessageLimit = 0, 
            bool runAsync = false, 
            IAmAChannelFactory channelFactory = null, 
            OnMissingChannel makeChannels = OnMissingChannel.Create) 
            : base(dataType, name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, 
                requeueDelayInMilliseconds, unacceptableMessageLimit, runAsync, channelFactory, makeChannels)
        {
        }
    }

    public class RedisSubscription<T> : RedisSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        public RedisSubscription(
            SubscriptionName name = null, 
            ChannelName channelName = null, 
            RoutingKey routingKey = null, 
            int bufferSize = 1, 
            int noOfPerformers = 1, 
            int timeoutInMilliseconds = 300, 
            int requeueCount = -1, 
            int requeueDelayInMilliseconds = 0, 
            int unacceptableMessageLimit = 0, 
            bool runAsync = false, 
            IAmAChannelFactory channelFactory = null, 
            OnMissingChannel makeChannels = OnMissingChannel.Create) 
            : base(typeof(T), name, channelName, routingKey, bufferSize, noOfPerformers, timeoutInMilliseconds, requeueCount, 
                requeueDelayInMilliseconds, unacceptableMessageLimit, runAsync, channelFactory, makeChannels)
        {
        }
    }
}
