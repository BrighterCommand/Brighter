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

namespace Paramore.Brighter.MessagingGateway.RMQ.Async
{
    public class RmqSubscription : Subscription
    {

        /// <summary>
        /// The name of  the queue to send rejects messages to
        /// </summary>
        public ChannelName? DeadLetterChannelName { get; }

        /// <summary>
        /// The routing key for dead letter messages
        /// </summary>
        public RoutingKey? DeadLetterRoutingKey { get; }
        
        /// <summary>
        /// Is the channel mirrored across node in the cluster
        /// Required when the API for queue creation in the Message Oriented Middleware needs us to set the value
        /// on channel (queue) creation. For example, RMQ version 2.X set high availability via the client API
        /// though it has moved to policy in versions 3+ 
        /// </summary>
        public bool HighAvailability { get; }
        
        /// <summary>
        /// Gets a value indicating whether this channel definition should survive restarts of the broker.
        /// </summary>
        /// <value><c>true</c> if this definition is durable; otherwise, <c>false</c>.</value>
        public bool IsDurable { get; }

        /// <summary>
        /// The maximum number of messages on the queue before we begin to reject messages
        /// </summary>
        public int? MaxQueueLength { get; }
        
        /// <summary>
        /// How long does a message live on the queue, before expiring?
        /// A null value, the default, is infinite
        /// </summary>
        public TimeSpan? Ttl { get; }
        
        /// <summary>
        /// The type of queue to use - Classic or Quorum
        /// </summary>
        public QueueType QueueType { get; }
        
        /// <inheritdoc />
        public override Type ChannelFactoryType => typeof(ChannelFactory);

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="requestType">Type of the data.</param>
        /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="requestType"/> if null</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The delay to the delivery of a requeue message; defaults to 0</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="isDurable">The durability of the queue definition in the broker.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="highAvailability">Should we mirror the queue over multiple nodes</param>
        /// <param name="deadLetterChannelName">The dead letter channel </param>
        /// <param name="deadLetterRoutingKey">The routing key for dead letters</param>
        /// <param name="ttl">Time to live in ms of a message on a queue; null (the default) is infinite</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        /// <param name="maxQueueLength">The maximum number of messages in a queue before we reject messages; defaults to no limit</param>
        /// <param name="queueType">The type of queue to use - Classic or Quorum; defaults to Classic</param>
        public RmqSubscription(SubscriptionName subscriptionName,
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
            bool isDurable = false,
            MessagePumpType messagePumpType = MessagePumpType.Unknown,
            IAmAChannelFactory? channelFactory = null,
            bool highAvailability = false,
            ChannelName? deadLetterChannelName = null,
            RoutingKey? deadLetterRoutingKey = null,
            TimeSpan? ttl = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null,
            int? maxQueueLength = null,
            QueueType queueType = QueueType.Classic) 
            : base(subscriptionName, channelName, routingKey, requestType, getRequestType, bufferSize, noOfPerformers, timeOut, requeueCount, requeueDelay, unacceptableMessageLimit, messagePumpType, channelFactory, makeChannels, emptyChannelDelay, channelFailureDelay)
        {
            DeadLetterRoutingKey = deadLetterRoutingKey;
            DeadLetterChannelName = deadLetterChannelName;
            HighAvailability = highAvailability;
            IsDurable = isDurable;
            Ttl = ttl;
            MaxQueueLength = maxQueueLength;
            QueueType = queueType;
        }
    }

    public class RmqSubscription<T> : RmqSubscription where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
        /// <param name="bufferSize">The number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10</param>
        /// <param name="noOfPerformers">The no of threads reading this channel.</param>
        /// <param name="timeOut">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="isDurable">The durability of the queue definition in the broker.</param>
        /// <param name="messagePumpType">Is this channel read asynchronously</param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="highAvailability">Should we mirror the queue over multiple nodes</param>
        /// <param name="deadLetterChannelName">The dead letter channel </param>
        /// <param name="deadLetterRoutingKey">The routing key for dead letters</param>
        /// <param name="ttl">Time to live in ms of a message on a queue; null (the default) is infinite</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        /// <param name="queueType">The type of queue to use - Classic or Quorum; defaults to Classic</param>
        public RmqSubscription(
            SubscriptionName? subscriptionName = null,
            ChannelName? channelName = null,
            RoutingKey? routingKey = null,
            Func<Message, Type>? getRequestType = null,
            int bufferSize = 1,
            int noOfPerformers = 1,
            TimeSpan? timeOut = null,
            int requeueCount = -1,
            TimeSpan? requeueDelay = null,
            int unacceptableMessageLimit = 0,
            bool isDurable = false,
            MessagePumpType messagePumpType = MessagePumpType.Proactor,
            IAmAChannelFactory? channelFactory = null,
            bool highAvailability = false,
            ChannelName? deadLetterChannelName = null,
            RoutingKey? deadLetterRoutingKey = null,
            TimeSpan? ttl = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null,
            QueueType queueType = QueueType.Classic)
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
                isDurable, 
                messagePumpType, 
                channelFactory, 
                highAvailability, 
                deadLetterChannelName, 
                deadLetterRoutingKey,
                ttl, 
                makeChannels, 
                emptyChannelDelay, 
                channelFailureDelay, 
                null, 
                queueType)
        { }

    }

}
