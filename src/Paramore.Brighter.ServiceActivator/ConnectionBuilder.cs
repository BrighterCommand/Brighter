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
        private int _milliseconds = 300;
        private string _routingKey;
        private int _buffersize = 1;
        private bool _isHighAvailability = false;
        private int _unacceptableMessageLimit = 0;
        private bool _isAsync = false;
        private bool _isDurable = false;
        private int _lockTimeout = 10;
        private OnMissingChannel _makeChannel = OnMissingChannel.Create;
        private int _noOfPeformers = 1;
        private int _requeueCount = -1;
        private int _requeueDelayInMilliseconds = 0;
        private ConnectionBuilder() {}

        public static IConnectionBuilderName With => new ConnectionBuilder();

        /// <summary>
        /// The name of the connection - used for identification
        /// </summary>
        /// <param name="name">The name to give this connection</param>
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
        /// <param name="millisecondTimeout">The number of milliseconds to timeout (defaults to 300)</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild Timeout(int millisecondTimeout)
        {
            _milliseconds = millisecondTimeout;
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
        /// Whether the queue should be mirrored across nodes on the broker
        /// </summary>
        /// <param name="highAvailability">Should we mirror queues (defaults to false)</param>
        public IConnectionBuilderOptionalBuild HighAvailability(bool isHighAvailability)
        {
            _isHighAvailability = isHighAvailability;
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
        /// is the queue definition persisted in the Broker? Used as a setting by RMQ, could be purposed to other middleware with transient queue definitions
        /// </summary>
        /// <param name="isDurable">Should we persist the queue definition. Defaults to false</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild IsDurable(bool isDurable)
        {
            _isDurable = isDurable;
            return this;
        }

        /// <summary>
        /// How long should should a lock be held before it times out and  makes a queue item available to be read by others
        /// Supported by platforms like SQS which calls it VisibilityTimeout
        /// </summary>
        /// <param name="lockTimeout">how long to time out for, defaults to 10 seconds</param>
        /// <returns></returns>
        public IConnectionBuilderOptionalBuild LockTimeout(int lockTimeout)
        {
            _lockTimeout = lockTimeout;
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
        /// <param name="requeueCount">The number of retries. Defauls to -1</param>
        public IConnectionBuilderOptionalBuild RequeueCount(int requeueCount)
        {
            _requeueCount = requeueCount;
            return this;
        }

        /// <summary>
        /// How long to delay before re-queuing a failed message
        /// </summary>
        /// <param name="millisecondsDelay">The delay in milliseconds before requeueing. Default is 0, or no delay</param>
        public IConnectionBuilderOptionalBuild RequeueDelayInMilliseconds(int millisecondsDelay)
        {
            _requeueDelayInMilliseconds = millisecondsDelay;
            return this;
        }

        public Connection Build()
        {
            return new Connection(_type,
                new ConnectionName(_name),
                new ChannelName(_channelName),
                new RoutingKey(_routingKey),
                channelFactory:_channelFactory,
                timeoutInMilliseconds: _milliseconds,
                bufferSize: _buffersize,
                noOfPerformers: _noOfPeformers,
                requeueCount: _requeueCount,
                requeueDelayInMilliseconds: _requeueDelayInMilliseconds,
                unacceptableMessageLimit: _unacceptableMessageLimit,
                isDurable: _isDurable,
                isAsync: _isAsync,
                highAvailability: _isHighAvailability,
                lockTimeout:_lockTimeout,
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
            Connection Build();
            
            /// <summary>
            /// Gets the timeout in milliseconds that we use to infer that nothing could be read from the channel i.e. is empty
            /// or busy
            /// </summary>
            /// <value>The timeout in miliseconds.</value>
            IConnectionBuilderOptionalBuild Timeout(int millisecondTimeout);

            /// <summary>
            /// How many messages do we store in the channel at any one time. When we read from a broker we need to balance
            /// supporting fairness amongst multiple consuming threads (if any) and latency from reading from the broker
            /// Must be greater than 1 and less than 10.
            /// </summary>
            IConnectionBuilderOptionalBuild BufferSize(int bufferSize);

            /// <summary>
            /// Is the channel mirrored across node in the cluster
            /// Required when the API for queue creation in the Message Oriented Middleware needs us to set the value
            /// on channel (queue) creation. For example, RMQ version 2.X set high availability via the client API
            /// though it has moved to policy in versions 3+ 
            /// </summary>
            IConnectionBuilderOptionalBuild HighAvailability(bool isHighAvailability);
            
            /// <summary>
            /// Gets the number of messages before we will terminate the channel due to high error rates
            /// </summary>
            IConnectionBuilderOptionalBuild UnacceptableMessageLimit(int unacceptableMessageLimit);

            /// <summary>
            /// Gets a value indicating whether this connection should use an asynchronous pipeline
            /// If it does it will process new messages from the queue whilst awaiting in prior messages' pipelines
            /// This increases throughput (although it will no longer throttle use of the resources on the host machine).
            /// </summary>
            /// <value><c>true</c> if this instance should use an asynchronous pipeline; otherwise, <c>false</c></value>
            IConnectionBuilderOptionalBuild IsAsync(bool isAsync);

            /// <summary>
            /// Gets a value indicating whether this channel definition should survive restarts of the broker.
            /// Used on RMQ versions prior to 4.0 but now set by policy, retained for possible future use or backward compat
            /// </summary>
            /// <value><c>true</c> if this definition is durable; otherwise, <c>false</c>.</value>
            IConnectionBuilderOptionalBuild IsDurable(bool isDurable);
            
            /// <summary>
            /// For some Message Oriented Middleware this governs how long a 'lock' is held on a message for one consumer
            /// to process. For example SQS
            /// </summary>
            IConnectionBuilderOptionalBuild LockTimeout(int lockTimeout);
            
            /// <summary>
            /// Should we declare infrastructure, or should we just validate that it exists, and assume it is declared elsewhere
            /// </summary>
            IConnectionBuilderOptionalBuild MakeChannels(OnMissingChannel onMissingChannel);
            
            /// <summary>
            /// Gets the no of threads that we will use to read from  this channel.
            /// </summary>
            /// <value>The no of peformers.</value>
            IConnectionBuilderOptionalBuild NoOfPeformers(int noOfPerformers);
            
            /// <summary>
            /// Gets or sets the number of times that we can requeue a message before we abandon it as poison pill.
            /// </summary>
            /// <value>The requeue count.</value>
            IConnectionBuilderOptionalBuild RequeueCount(int requeueCount);
            
            /// <summary>
            /// Gets or sets number of milliseconds to delay delivery of re-queued messages.
            /// </summary>
            IConnectionBuilderOptionalBuild RequeueDelayInMilliseconds(int millisecondsDelay);
        }
    }
}
