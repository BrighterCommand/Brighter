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
        public IAmAChannelFactory? ChannelFactory { get; set; }

        /// <summary>
        /// Gets the <see cref="ChannelName"/> we use for this channel. In platforms where queues have names, will be used as the name of the queue
        /// </summary>
        /// <remarks>
        /// Note that this is not the logical endpoint, the topic, that the channel consumes from for pub-sub, that is the <see cref="RoutingKey"/>
        /// </remarks>
        /// <value>The name.</value>
        public ChannelName ChannelName { get; set; }

        /// <summary>
        /// How long to pause when there is a channel failure in milliseconds
        /// </summary>
        /// <value>The <see cref="TimeSpan"/> for the delay</value>
        public TimeSpan ChannelFailureDelay { get; set; }

        /// <summary>
        /// Gets the type of the <see cref="IRequest"/> that <see cref="Message"/>s on the <see cref="Channel"/> can be translated into.
        /// </summary>
        /// <value>The <see cref="Type"/> of the data.</value>
        public Type? RequestType { get; }

        /// <summary>
        /// How long to pause when a channel is empty in milliseconds
        /// </summary>
        /// <value>The <see cref="TimeSpan"/> of the channel delay</value>
        public TimeSpan EmptyChannelDelay { get; set; }
        
        
        /// <summary>
        /// Determines the type of the message on the channel.
        /// For a <see href = "https://www.enterpriseintegrationpatterns.com/patterns/messaging/DatatypeChannel.html">Datatype Channel</see>, <see cref="RequestType"/> is the type of the <see cref="IRequest"/> that the channel will read and we just return that.
        /// If you want to determine the type of the <see cref="IRequest"/> from the <see cref="Message"/>, you can set this property to a function that takes a <see cref="Message"/> and returns a <see cref="Type"/>.
        /// A typical strategy is to use the <see cref="MessageHeader"/> to read the Cloud Event Type and use that to look up the <see cref="Type"/>.
        /// We default to a Datatype Channel, where the <see cref="RequestType"/> is the type of the <see cref="IRequest"/> that the channel will read.
        /// </summary>
        /// <remarks>We recommend using a RequestType channel, as it is operationally easier to reason about, but we support other strategies if needed</remarks>
        public Func<Message, Type> MapRequestType { get; set; } 

        /// <summary>
        /// Should we declare infrastructure, or should we just validate that it exists, and assume it is declared elsewhere
        /// </summary>
        /// <value>The <see cref="OnMissingChannel"/> policy for handling missing channels.</value>
        public OnMissingChannel MakeChannels { get; set; }

        /// <summary>
        /// Gets or sets the name of the subscription, for identification.
        /// </summary>
        /// <value>The <see cref="SubscriptionName"/> of the Subscription</value>
        public SubscriptionName Name { get; }

        /// <summary>
        /// Gets the no of threads that we will use to read from  this channel.
        /// </summary>
        /// <value>The <see cref="int"/> for number of performers.</value>
        public int NoOfPerformers { get; private set; }

        /// <summary>
        /// Gets or sets the number of times that we can requeue a message before we abandon it as poison pill.
        /// </summary>
        /// <value>The  <see cref="int"/> for the requeue count.</value>
        public int RequeueCount { get; }

        /// <summary>
        /// Gets or sets the delay for delivery of re-queued messages.
        /// </summary>
        /// <value>A <see cref="TimeSpan"/> specifying how long to delay requeued messages.</value>
        public TimeSpan RequeueDelay { get; }

        /// <summary>
        /// Gets or sets the routing key or topic that this channel subscribes to on the broker.
        /// </summary>
        /// <value>The <see cref="RoutingKey"/> that specifies the topic or routing pattern for this subscription.</value>
        /// <remarks>
        ///  In many platforms, a queue subscribes to the topic. In that case the <see cref="ChannelName"/> gives the queue name
        /// whilst this is the topic to which that queue subscribes.
        /// </remarks>
        public RoutingKey RoutingKey { get; set; }

        /// <summary>
        /// Gets a value indicating whether this subscription should use an asynchronous pipeline.
        /// If it does it will process new messages from the queue whilst awaiting in prior messages' pipelines.
        /// This increases throughput (although it will no longer throttle use of the resources on the host machine).
        /// </summary>
        /// <value>A <see cref="MessagePumpType"/> indicating the type of message pump to use for this subscription.</value>
        public MessagePumpType MessagePumpType { get; }

        /// <summary>
        /// Gets the timeout that we use to infer that nothing could be read from the channel i.e. is empty or busy.
        /// </summary>
        /// <value>The <see cref="TimeSpan"/> timeout value.</value>
        public TimeSpan TimeOut { get; }

        /// <summary>
        /// Gets the number of messages before we will terminate the channel due to high error rates.
        /// </summary>
        /// <value>The <see cref="int"/> maximum number of unacceptable messages before stopping the channel.</value>
        public int UnacceptableMessageLimit { get; }

        /// <summary>
        /// Gets the factory type for this subscription. Internal use only.
        /// </summary>
        /// <value>The <see cref="Type"/> of the channel factory, defaults to <see cref="InMemoryChannelFactory"/>.</value>
        /// <remarks>
        /// It'll be used by the <see cref="CombinedChannelFactory"/>.
        /// </remarks>
        public virtual Type ChannelFactoryType => typeof(InMemoryChannelFactory);

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="subscriptionName">The <see cref="SubscriptionName"/> for identification. Defaults to the data type's full name.</param>
        /// <param name="channelName">The <see cref="ChannelName"/> for the channel. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The <see cref="RoutingKey"/> for message routing. Defaults to the data type's full name.</param>
        /// <param name="requestType">The <see cref="Type"/> of the data that this subscription handles.</param>
        /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="RequestType"/> if null</param>
        /// <param name="bufferSize">The <see cref="int"/> number of messages to buffer at any one time, also the number of messages to retrieve at once. Min of 1 Max of 10.</param>
        /// <param name="noOfPerformers">The <see cref="int"/> number of threads reading this channel.</param>
        /// <param name="timeOut">The <see cref="TimeSpan"/> timeout for the subscription to consider the queue empty and pause.</param>
        /// <param name="requeueCount">The <see cref="int"/> number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The <see cref="TimeSpan"/> to delay the delivery of a requeue message for.</param>
        /// <param name="unacceptableMessageLimit">The <see cref="int"/> number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="messagePumpType">The <see cref="MessagePumpType"/> indicating whether this channel is read asynchronously.</param>
        /// <param name="channelFactory">The <see cref="IAmAChannelFactory"/> to create channels for Consumer.</param>
        /// <param name="makeChannels">The <see cref="OnMissingChannel"/> policy - should we make channels if they don't exist, defaults to creating.</param>
        /// <param name="emptyChannelDelay">The <see cref="TimeSpan"/> to pause when a channel is empty in milliseconds.</param>
        /// <param name="channelFailureDelay">The <see cref="TimeSpan"/> to pause when there is a channel failure in milliseconds.</param>
        /// <exception cref="ConfigurationException">Thrown when <paramref name="messagePumpType"/> is <see cref="MessagePumpType.Unknown"/>.</exception>
        public Subscription(
            SubscriptionName subscriptionName,
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
            MessagePumpType messagePumpType = MessagePumpType.Unknown,
            IAmAChannelFactory? channelFactory = null,
            OnMissingChannel makeChannels = OnMissingChannel.Create,
            TimeSpan? emptyChannelDelay = null,
            TimeSpan? channelFailureDelay = null)
        {
            if (messagePumpType == MessagePumpType.Unknown)
                throw new ConfigurationException("You must set a message pump type: use Reactor for sync pipelines; use Proactor for async pipelines");
            
            if (requestType is null && getRequestType is null)
                throw new ConfigurationException("You must set a request type or a function to map a message to a request type");
            
            RequestType = requestType;
            MapRequestType = getRequestType ?? (message => requestType!);
            Name = subscriptionName;
            ChannelName = channelName;
            RoutingKey = routingKey; 
            BufferSize = bufferSize;
            NoOfPerformers = noOfPerformers;
            timeOut ??= TimeSpan.FromMilliseconds(300);
            TimeOut = timeOut.Value;
            RequeueCount = requeueCount;
            requeueDelay ??= TimeSpan.Zero;
            RequeueDelay = requeueDelay.Value;
            UnacceptableMessageLimit = unacceptableMessageLimit;
            MessagePumpType = messagePumpType;
            ChannelFactory = channelFactory;
            MakeChannels = makeChannels;
            EmptyChannelDelay = emptyChannelDelay ?? TimeSpan.FromMilliseconds(500);
            ChannelFailureDelay = channelFailureDelay ?? TimeSpan.FromMilliseconds(1000);
        }

        /// <summary>
        /// Sets the number of performers (worker threads) for this subscription.
        /// </summary>
        /// <param name="numberOfPerformers">The <see cref="int"/> number of performers to set. Negative values will be set to 0.</param>
        public void SetNumberOfPerformers(int numberOfPerformers)
        {
            NoOfPerformers = numberOfPerformers < 0 ? 0 : numberOfPerformers;
        }

    }

    /// <summary>
    /// A generic version of <see cref="Subscription"/> that is strongly typed to a specific <see cref="IRequest"/> type.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IRequest"/> that this subscription handles.</typeparam>
    /// <remarks>
    /// This generic version provides compile-time type safety and is recommended when the request type is known at compile time.
    /// </remarks>
    public class Subscription<T> : Subscription
        where T : IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class with data type T.
        /// </summary>
        /// <param name="subscriptionName">The name. Defaults to the data type's full name.</param>
        /// <param name="channelName">The channel name. Defaults to the data type's full name.</param>
        /// <param name="routingKey">The routing key. Defaults to the data type's full name.</param>
        /// <param name="noOfPerformers">The no of performers.</param>
        /// <param name="getRequestType">The <see cref="Func{T,TResult}"/> that determines how we map a message to a type. Defaults to returning the <see cref="T"/> if null</param>
        /// <param name="bufferSize">The number of messages to buffer on the channel</param>
        /// <param name="timeOut">The timeout before we consider the subscription empty and pause</param>
        /// <param name="requeueCount">The number of times you want to requeue a message before dropping it.</param>
        /// <param name="requeueDelay">The delay the delivery of a requeue message; defaults to 0ms</param>
        /// <param name="unacceptableMessageLimit">The number of unacceptable messages to handle, before stopping reading from the channel.</param>
        /// <param name="messagePumpType"></param>
        /// <param name="channelFactory">The channel factory to create channels for Consumer.</param>
        /// <param name="makeChannels">Should we make channels if they don't exist, defaults to creating</param>
        /// <param name="emptyChannelDelay">How long to pause when a channel is empty in milliseconds</param>
        /// <param name="channelFailureDelay">How long to pause when there is a channel failure in milliseconds</param>
        public Subscription(
            SubscriptionName? subscriptionName = null,
            ChannelName? channelName = null,
            RoutingKey? routingKey = null,
            int noOfPerformers = 1,
            Func<Message, Type>? getRequestType = null,
            int bufferSize = 1,
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
                subscriptionName  ?? new SubscriptionName(typeof(T).FullName!),
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
                makeChannels, emptyChannelDelay, channelFailureDelay)
        {
        }
    }
}
