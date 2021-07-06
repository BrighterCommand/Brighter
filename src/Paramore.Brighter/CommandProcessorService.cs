using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.Logging;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Provide services to CommandProcessor that persist across the lifetime of the application. Allows separation from elements that have a lifetime linked
    /// to the scope of a request, or are transient for DI purposes
    /// </summary>
    internal class CommandProcessorService
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        private readonly IAmAMessageMapperRegistry _mapperRegistry;
        private readonly IAmASubscriberRegistry _subscriberRegistry;
        private readonly IAmAHandlerFactoryAsync _asyncHandlerFactory;
        private readonly IAmARequestContextFactory _requestContextFactory;
        private readonly IPolicyRegistry<string> _policyRegistry;
        private readonly int _outboxTimeout;
        private readonly IAmAnOutbox<Message> _outBox;
        private readonly IAmAnOutboxAsync<Message> _asyncOutbox;
        private readonly InboxConfiguration _inboxConfiguration;
        private readonly IAmAFeatureSwitchRegistry _featureSwitchRegistry;
        private DateTime _lastOutStandingMessageCheckAt = DateTime.UtcNow;
        
        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static bool should be made thread-safe by locking the object
        private readonly object _checkOutStandingMessagesObject = new object();

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish
        private int _outStandingCount;

        // the following are not readonly to allow setting them to null on dispose
        private IAmAMessageProducer _messageProducer;
        private IAmAMessageProducerAsync _asyncMessageProducer;
        private bool _disposed;

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKER = "Paramore.Brighter.CommandProcessor.CircuitBreaker";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IAmAPolicyRegistry"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICY = "Paramore.Brighter.CommandProcessor.RetryPolicy";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines for how long to break the circuit when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string CIRCUITBREAKERASYNC = "Paramore.Brighter.CommandProcessor.CircuitBreaker.Async";

        /// <summary>
        /// Use this as an identifier for your <see cref="Policy"/> that determines the retry strategy when communication with the Work Queue fails.
        /// Register that policy with your <see cref="IPolicyRegistry{TKey}"/> such as <see cref="PolicyRegistry"/>
        /// You can use this an identifier for you own policies, if your generic policy is the same as your Work Queue policy.
        /// </summary>
        public const string RETRYPOLICYASYNC = "Paramore.Brighter.CommandProcessor.RetryPolicy.Async";

        public CommandProcessorService(
            IAmAMessageMapperRegistry mapperRegistry, 
            IAmASubscriberRegistry subscriberRegistry, 
            IPolicyRegistry<string> policyRegistry, 
            int outboxTimeout, 
            IAmAnOutbox<Message> outBox, 
            IAmAnOutboxAsync<Message> asyncOutbox, 
            InboxConfiguration inboxConfiguration, 
            IAmAFeatureSwitchRegistry featureSwitchRegistry, 
            IAmAMessageProducer messageProducer, 
            IAmAMessageProducerAsync asyncMessageProducer 
            )
        {
            _mapperRegistry = mapperRegistry;
            _subscriberRegistry = subscriberRegistry;
            _policyRegistry = policyRegistry;
            _outboxTimeout = outboxTimeout;
            _outBox = outBox;
            _asyncOutbox = asyncOutbox;
            _inboxConfiguration = inboxConfiguration;
            _featureSwitchRegistry = featureSwitchRegistry;
            _messageProducer = messageProducer;
            _asyncMessageProducer = asyncMessageProducer;
        }


        internal void AssertValidSendPipeline<T>(T command, int handlerCount) where T : class, IRequest
        {
            s_logger.LogInformation("Found {HandlerCount} pipelines for command: {Type} {Id}", handlerCount, typeof(T), command.Id);

            if (handlerCount > 1)
                throw new ArgumentException($"More than one handler was found for the typeof command {typeof(T)} - a command should only have one handler.");
            if (handlerCount == 0)
                throw new ArgumentException($"No command handler was found for the typeof command {typeof(T)} - a command should have exactly one handler.");
        }

        internal void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (_outBox != null || _asyncOutbox != null);
            if (!hasOutBox)
                return;

            int maxOutStandingMessages = -1;
            if (_messageProducer != null)
                maxOutStandingMessages = _messageProducer.MaxOutStandingMessages;

            if (_asyncMessageProducer != null)
                maxOutStandingMessages = _asyncMessageProducer.MaxOutStandingMessages;

            s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit = maxOutStandingMessages != -1 && _outStandingCount > maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException($"The outbox limit of {maxOutStandingMessages} has been exceeded");
        }

        internal void CheckOutstandingMessages()
        {
            if (_messageProducer == null)
                return;

            var now = DateTime.UtcNow;
            var checkInterval = TimeSpan.FromMilliseconds(_messageProducer.MaxOutStandingCheckIntervalMilliSeconds);


            var timeSinceLastCheck = now - _lastOutStandingMessageCheckAt;
            s_logger.LogDebug("Time since last check is {SecondsSinceLastCheck} seconds.", timeSinceLastCheck.TotalSeconds);
            if (timeSinceLastCheck < checkInterval)
            {
                s_logger.LogDebug($"Check not ready to run yet");
                return;
            }

            s_logger.LogDebug("Running outstanding message check at {MessageCheckTime} after {SecondsSinceLastCheck} seconds wait", DateTime.UtcNow, timeSinceLastCheck.TotalSeconds);
            //This is expensive, so use a background thread
            Task.Run(() => OutstandingMessagesCheck());
            _lastOutStandingMessageCheckAt = DateTime.UtcNow;
        }

        internal bool ConfigureAsyncPublisherCallbackMaybe()
        {
            if (_asyncMessageProducer == null)
                return false;

            if (_asyncMessageProducer is ISupportPublishConfirmation producer)
            {
                producer.OnMessagePublished += async delegate(bool success, Guid id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id.ToString());
                        if (_asyncOutbox != null)
                            await RetryAsync(async ct => await _asyncOutbox.MarkDispatchedAsync(id, DateTime.UtcNow));
                    }
                };
                return true;
            }

            return false;
        }

        internal bool ConfigurePublisherCallbackMaybe()
        {
            if (_messageProducer is ISupportPublishConfirmation producer)
            {
                producer.OnMessagePublished += delegate(bool success, Guid id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id.ToString());
                        if (_outBox != null)
                            Retry(() => _outBox.MarkDispatched(id, DateTime.UtcNow));
                    }
                };
                return true;
            }

            return false;
        }

        internal void OutstandingMessagesCheck()
        {
            if (Monitor.TryEnter(_checkOutStandingMessagesObject))
            {

                s_logger.LogDebug("Begin count of outstanding messages");
                try
                {
                    if ((_outBox != null) && (_outBox is IAmAnOutboxViewer<Message> outboxViewer))
                    {
                        _outStandingCount = outboxViewer
                            .OutstandingMessages(_messageProducer.MaxOutStandingCheckIntervalMilliSeconds)
                            .Count();
                        return;
                    }

                    //TODO: There is no async version of this call at present; the thread here means that won't hurt if implemented
                    if ((_asyncOutbox != null) && (_asyncOutbox is IAmAnOutboxViewer<Message> asyncOutboxViewer))
                    {
                        _outStandingCount = asyncOutboxViewer
                            .OutstandingMessages(_messageProducer.MaxOutStandingCheckIntervalMilliSeconds)
                            .Count();
                        return;
                    }

                    if ((_outBox == null) && (_asyncOutbox == null))
                        _outStandingCount = 0;

                }
                catch (Exception ex)
                {
                    //if we can't talk to the outbox, we would swallow the exception on this thread
                    //by setting the _outstandingCount to -1, we force an exception
                    s_logger.LogError(ex,"Error getting outstanding message count, reset count");
                    _outStandingCount = 0;
                }
                finally
                {
                    s_logger.LogDebug("Current outstanding count is {OutStandingCount}", _outStandingCount);
                    Monitor.Exit(_checkOutStandingMessagesObject);
                }
            }
        }

        internal bool Retry(Action send)
        {
            var policy = _policyRegistry.Get<Policy>(RETRYPOLICY);
            var result = policy.ExecuteAndCapture(send);
            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException,"Exception whilst trying to publish message");
                    CheckOutstandingMessages();
                }

                return false;
            }

            return true;
        }

        internal async Task<bool> RetryAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await _policyRegistry.Get<AsyncPolicy>(RETRYPOLICYASYNC)
                .ExecuteAndCaptureAsync(send, cancellationToken, continueOnCapturedContext)
                .ConfigureAwait(continueOnCapturedContext);

            if (result.Outcome != OutcomeType.Successful)
            {
                if (result.FinalException != null)
                {
                    s_logger.LogError(result.FinalException, "Exception whilst trying to publish message" );
                    CheckOutstandingMessages();
                }

                return false;
            }

            return true;
        }
        
    }
}
