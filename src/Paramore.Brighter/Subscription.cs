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

namespace Paramore.Brighter
{
    /// <summary>
    /// Class Subscription.
    /// A <see cref="Subscription"/> holds the configuration details of the relationship between a channel provided by a broker, and a <see cref="Command"/> or <see cref="Event"/>. 
    /// It holds information on the number of threads to use to process <see cref="Message"/>s on the channel, turning them into <see cref="Command"/>s or <see cref="Event"/>s
    /// A Subscription is not Gateway specific configuration, that belongs in a class derived from <see cref="IAmGatewayConfiguration"/>
    /// </summary>
    public class Subscription
    {
        /// <summary>
        /// How many messages do we store in the channel at any one time. When we read from a broker we need to balance
        /// supporting fairness amongst multiple consuming threads (if any) and latency from reading from the broker
        /// Must be greater than 1 and less than 10.
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// Gets the channel.
        /// </summary>
        /// <value>The channel.</value>
        public IAmAChannelFactory ChannelFactory { get; set; }

        /// <summary>
        /// Gets the name we use for this channel. In platforms where queues have names, will be used as the name of the queue
        /// Note that this is not the logical endpoint that the channel consumes from, that is the RoutingKey
        /// </summary>
        /// <value>The name.</value>
        public ChannelName ChannelName { get; set; }

       /// <summary>
       /// How long to pause when there is a channel failure in milliseconds
       /// </summary>
       public int ChannelFailureDelay { get; set; }
       
     /// <summary>
        /// Gets the type of the <see cref="IRequest"/> that <see cref="Message"/>s on the <see cref="Channel"/> can be translated into.
        /// </summary>
        /// <value>The type of the data.</value>
        public Type DataType { get; }

       /// <summary>
        /// How long to pause when a channel is empty in milliseconds
        /// </summary>
        public int EmptyChannelDelay { get; set; }
       
    /// <summary>
        /// Should we declare infrastructure, or should we just validate that it exists, and assume it is declared elsewhere
        /// </summary>
        public OnMissingChannel MakeChannels { get; }

         /// <summary>
        /// Gets or sets the name of the subscription, for identification.
        /// </summary>
        /// <value>The name.</value>
        public SubscriptionName Name { get; }

        /// <summary>
        /// Gets the no of threads that we will use to read from  this channel.
        /// </summary>
        /// <value>The no of peformers.</value>
        public int NoOfPeformers { get; private set; }

        /// <summary>
        /// Gets or sets the number of times that we can requeue a message before we abandon it as poison pill.
        /// </summary>
        /// <value>The requeue count.</value>
        public int RequeueCount { get; }

        /// <summary>
        /// Gets or sets number of milliseconds to delay delivery of re-queued messages.
        /// </summary>
        public int RequeueDelayInMilliseconds { get; }

        /// <summary>
        /// Gets or sets the routing key or topic that this channel subscribes to on the broker.
        /// </summary>
        /// <value>The name.</value>
        public RoutingKey RoutingKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether this subscription should use an asynchronous pipeline
        /// If it does it will process new messages from the queue whilst awaiting in prior messages' pipelines
        /// This increases throughput (although it will no longer throttle use of the resources on the host machine).
        /// </summary>
        /// <value><c>true</c> if this instance should use an asynchronous pipeline; otherwise, <c>false</c></value>
        public bool RunAsync { get; }

        /// <summary>
        /// Gets the timeout in milliseconds that we use to infer that nothing could be read from the channel i.e. is empty
        /// or busy
        /// </summary>

        /// <value>The timeout in miliseconds.</value>
        [Obsolete("Use TimeoutInMilliseconds")]
        public int TimeoutInMiliseconds { get => TimeoutInMilliseconds; }

        /// <summary>
        /// Gets the timeout in milliseconds that we use to infer that nothing could be read from the channel i.e. is empty
        /// or busy
        /// </summary>
        /// <value>The timeout in milliseconds.</value>
        public int TimeoutInMilliseconds { get; }

        /// <summary>
        /// Gets the number of messages before we will terminate the channel due to high error rates
        /// </summary>
        public int UnacceptableMessageLimit { get; }

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
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public Subscription(
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
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            int emptyChannelDelay = 500,
            int channelFailureDelay = 1000)
        {
            DataType = dataType;
            Name = name ?? new SubscriptionName(dataType.FullName);
            ChannelName = channelName ?? new ChannelName(dataType.FullName);
            RoutingKey = routingKey ?? new RoutingKey(dataType.FullName);
            BufferSize = bufferSize;
            NoOfPeformers = noOfPerformers;
            TimeoutInMilliseconds = timeoutInMilliseconds;
            RequeueCount = requeueCount;
            RequeueDelayInMilliseconds = requeueDelayInMilliseconds;
            UnacceptableMessageLimit = unacceptableMessageLimit;
            RunAsync = runAsync;
            ChannelFactory = channelFactory;
            MakeChannels = makeChannels;
            EmptyChannelDelay = emptyChannelDelay;
            ChannelFailureDelay = channelFailureDelay;
        }
        
        public void SetNumberOfPerformers(int numberOfPerformers)
        {
            NoOfPeformers = numberOfPerformers < 0 ? 0 : numberOfPerformers;
        }
    }

    public class Subscription<T> : Subscription
        where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class with data type T.
        /// </summary>
        /// <param name="name">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelayInMilliseconds">The number of milliseconds to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="runAsync"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public Subscription(
            SubscriptionName name = null,
            ChannelName channelName = null,
            RoutingKey routingKey = null,
            int noOfPerformers = 1,
            int bufferSize = 1,
            int timeoutInMilliseconds = 300,
            int requeueCount = -1,
            int requeueDelayInMilliseconds = 0,
            int unacceptableMessageLimit = 0,
            bool runAsync = false,
            IAmAChannelFactory channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            int emptyChannelDelay = 500,
            int channelFailureDelay = 1000)
            : base(
                typeof(T),
                name,
                channelName,
                routingKey,
                bufferSize,
                noOfPerformers,
                timeoutInMilliseconds,
                requeueCount,
                requeueDelayInMilliseconds,
                unacceptableMessageLimit,
                runAsync,
                channelFactory,
                makeChannels,
                emptyChannelDelay,
                channelFailureDelay)
        {
        }
    }
}
