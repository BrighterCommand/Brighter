using System;

namespace Paramore.Brighter.ServiceActivator
{
    public class ConnectionBuilder : 
        ConnectionBuilder.IConnectionBuilderName,
        ConnectionBuilder.IConnectionBuilderChannelFactory, 
        ConnectionBuilder.IConnectionBuilderChannelType,
        ConnectionBuilder.IConnectionBuilderChannelName,
        ConnectionBuilder.IConnectionBuilderRoutingKey,
        ConnectionBuilder.IConnectionBuilderOptionalBuild
    {
        private string _name;
        private IAmAChannelFactory _channelFactory;
        private Type _type;
        private string _channelName;
        private TimeSpan? _timeOut = null;
        private string _routingKey;
        private int _buffersize = 1;
        private int _unacceptableMessageLimit = 0;
        private bool _isAsync = false;
        private OnMissingChannel _makeChannel = OnMissingChannel.Create;
        private int _noOfPeformers = 1;
        private int _requeueCount = -1;
        private TimeSpan _requeueDelay = TimeSpan.Zero;
        private ConnectionBuilder() {}

        public static IConnectionBuilderName With => new ConnectionBuilder();

        /// <summary>
        /// The name of the subscription - used for identification
        /// </summary>
        /// <param name="name">The name to give this subscription</param>
        /// <returns></returns>
       public IConnectionBuilderChannelFactory ConnectionName(string name)
        {
            _name = name;
            return this;
        }

        /// <summary>
        /// How do we build instances of the channel - sometimes this may build a consumer that builds the channel indirectly
        /// </summary>
        /// <param name="channelFactory">The channel to use</param>
        /// <returns></returns>
        public IConnectionBuilderChannelType ChannelFactory(IAmAChannelFactory channelFactory)
        {
            _channelFactory = channelFactory;
            return this;
        }

        /// <summary>
        /// The data type of the channel
        /// </summary>
        /// <param name="type">The type that represents the type of the channel</param>
        /// <returns></returns>
        public IConnectionBuilderChannelName Type(Type type)
        {
            _type = type;
            return this;
        }

        /// <summary>
        /// What is the name of the channel
        /// </summary>
        /// <param name="name">The name for the channel</param>
        /// <returns></returns>
        public IConnectionBuilderRoutingKey ChannelName(string channelName)
        {
            _channelName = channelName;
            return this;
        }
        
        /// <summary>
        /// The routing key, or topic, that represents the channel in a broker
        /// </summary>
        /// <param name="routingKey"></param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild RoutingKey(string routingKey)
        {
            _routingKey = routingKey;
            return this;
        }

        /// <summary>
        /// The timeout for waiting for a message when polling a queue
        /// </summary>
        /// <param name="timeOut">The number of milliseconds to timeout (defaults to 300)</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild TimeOut(TimeSpan? timeOut = null)
        {
            timeOut ??= TimeSpan.FromMilliseconds(300);
            _timeOut = timeOut;
            return this;
        }

        /// <summary>
        /// Sets the size of the message buffer
        /// </summary>
        /// <param name="bufferSize">The number of messages to buffer (defaults to 1)</param>
        public IConnectionBuilderOptionalBuild BufferSize(int bufferSize)
        {
            _buffersize = bufferSize;
            return this;
        }

        /// <summary>
        /// How many unacceptable messages on a queue before we shut down to avoid taking good messages off the queue that should be recovered later
        /// </summary>
        /// <param name="unacceptableMessageLimit">The upper bound for unacceptable messages, 0, the default indicates no limit</param>
        public IConnectionBuilderOptionalBuild UnacceptableMessageLimit(int unacceptableMessageLimit)
        {
            _unacceptableMessageLimit = unacceptableMessageLimit;
            return this;
        }

        /// <summary>
        /// Is the pipeline that handles this message async?
        /// </summary>
        /// <param name="isAsync">True if it is an async pipeline. Defaults to false as less beneficial that might be guessed with an event loop</param>
        public IConnectionBuilderOptionalBuild IsAsync(bool isAsync)
        {
            _isAsync = isAsync;
            return this;
        }

        /// <summary>
        /// Should we create channels, or assume that they have been created separately and just confirm their existence and error if not available
        /// </summary>
        /// <param name="onMissingChannel">The action to take if a channel is missing. Defaults to create channel</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild MakeChannels(OnMissingChannel onMissingChannel)
        {
            _makeChannel = onMissingChannel;
            return this;
        }

        /// <summary>
        /// The number of threads to run, when you want to use a scale up approach to competing consumers
        /// Each thread is its own event loop - a performer
        /// </summary>
        /// <param name="noOfPerformers">How many threads to run, Defaults to 1</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild NoOfPeformers(int noOfPerformers)
        {
            _noOfPeformers = noOfPerformers;
            return this;
        }
        

        /// <summary>
        /// How many times to requeue a message before we give up on it. A count of -1 is infinite retries
        /// </summary>
        /// <param name="requeueCount">The number of retries. Defaults to -1</param>
        public IConnectionBuilderOptionalBuild RequeueCount(int requeueCount)
        {
            _requeueCount = requeueCount;
            return this;
        }

        /// <summary>
        /// How long to delay before re-queuing a failed message
        /// </summary>
        /// <param name="delay">The delay before requeueing. Default is 0, or no delay</param>
        public IConnectionBuilderOptionalBuild RequeueDelay(TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            _requeueDelay = delay.Value;
            return this;
        }

        public Subscription Build()
        {
            if (_type is null) throw new ArgumentException("Cannot build connection without a Type");
            if (_name is null) throw new ArgumentException("Cannot build connection without a Name");
            if (_channelName is null) throw new ArgumentException("Cannot build connection without a Channel Name");
            if (_routingKey is null) throw new ArgumentException("Cannot build connection without a Routing Key");
            
            return new Subscription(_type,
                new SubscriptionName(_name),
                new ChannelName(_channelName),
                new RoutingKey(_routingKey),
                channelFactory:_channelFactory,
                timeOut: _timeOut,
                bufferSize: _buffersize,
                noOfPerformers: _noOfPeformers,
                requeueCount: _requeueCount,
                requeueDelay: _requeueDelay,
                unacceptableMessageLimit: _unacceptableMessageLimit,
                runAsync: _isAsync,
                makeChannels:_makeChannel);
        }

        public interface IConnectionBuilderName
        {
            IConnectionBuilderChannelFactory ConnectionName(string name);
        }

        public interface IConnectionBuilderChannelFactory
        {
            IConnectionBuilderChannelType ChannelFactory(IAmAChannelFactory channelFactory);
        }

        public interface IConnectionBuilderChannelType
        {
            IConnectionBuilderChannelName Type(Type type);
        }

        public interface IConnectionBuilderChannelName
        {
            IConnectionBuilderRoutingKey ChannelName(string channelName);
        }

        public interface IConnectionBuilderRoutingKey
        {
            IConnectionBuilderOptionalBuild RoutingKey(string routingKey);
        }

        public interface IConnectionBuilderOptionalBuild
        {
            Subscription Build();
            
            /// <summary>
            /// Gets the timeout in milliseconds that we use to infer that nothing could be read from the channel i.e. is empty
            /// or busy
            /// </summary>
            /// <value>The timeout in milliseconds.</value>
            IConnectionBuilderOptionalBuild TimeOut(TimeSpan? timeOut);

            /// <summary>
            /// How many messages do we store in the channel at any one time. When we read from a broker we need to balance
            /// supporting fairness amongst multiple consuming threads (if any) and latency from reading from the broker
            /// Must be greater than 1 and less than 10.
            /// </summary>
            IConnectionBuilderOptionalBuild BufferSize(int bufferSize);

            /// <summary>
            /// Gets the number of messages before we will terminate the channel due to high error rates
            /// </summary>
            IConnectionBuilderOptionalBuild UnacceptableMessageLimit(int unacceptableMessageLimit);

            /// <summary>
            /// Gets a value indicating whether this subscription should use an asynchronous pipeline
            /// If it does it will process new messages from the queue whilst awaiting in prior messages' pipelines
            /// This increases throughput (although it will no longer throttle use of the resources on the host machine).
            /// </summary>
            /// <value><c>true</c> if this instance should use an asynchronous pipeline; otherwise, <c>false</c></value>
            IConnectionBuilderOptionalBuild IsAsync(bool isAsync);

            /// <summary>
            /// Should we declare infrastructure, or should we just validate that it exists, and assume it is declared elsewhere
            /// </summary>
            IConnectionBuilderOptionalBuild MakeChannels(OnMissingChannel onMissingChannel);
            
            /// <summary>
            /// Gets the no of threads that we will use to read from  this channel.
            /// </summary>
            /// <value>The no of performers.</value>
            IConnectionBuilderOptionalBuild NoOfPeformers(int noOfPerformers);
            
            /// <summary>
            /// Gets or sets the number of times that we can requeue a message before we abandon it as poison pill.
            /// </summary>
            /// <value>The requeue count.</value>
            IConnectionBuilderOptionalBuild RequeueCount(int requeueCount);
            
            /// <summary>
            /// Gets or sets number of milliseconds to delay delivery of re-queued messages.
            /// </summary>
            IConnectionBuilderOptionalBuild RequeueDelay(TimeSpan? delay);
        }
    }
}
