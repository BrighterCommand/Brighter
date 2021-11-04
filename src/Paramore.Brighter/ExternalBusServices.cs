using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter
{
    /// <summary>
    /// Provide services to CommandProcessor that persist across the lifetime of the application. Allows separation from elements that have a lifetime linked
    /// to the scope of a request, or are transient for DI purposes
    /// </summary>
    internal class ExternalBusServices : IDisposable
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CommandProcessor>();

        internal IPolicyRegistry<string> PolicyRegistry { get; set; }
        internal IAmAnOutboxSync<Message> OutBox { get; set; }
        internal IAmAnOutboxAsync<Message> AsyncOutbox { get; set; }

        internal int OutboxTimeout { get; set; } = 300;
        internal IAmAMessageProducerSync MessageProducerSync { get; set; }
        internal IAmAMessageProducerAsync AsyncMessageProducer { get; set; }
        
        private DateTime _lastOutStandingMessageCheckAt = DateTime.UtcNow;
        
        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static bool should be made thread-safe by locking the object
        private readonly object _checkOutStandingMessagesObject = new object();

        //Uses -1 to indicate no outbox and will thus force a throw on a failed publish
        private int _outStandingCount;
        private bool _disposed;
        
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;


            if (disposing)
            {
                MessageProducerSync?.Dispose();
                AsyncMessageProducer?.Dispose();
            }

            MessageProducerSync = null;
            AsyncMessageProducer = null;

            _disposed = true;
        }

        internal async Task AddToOutboxAsync<T>(T request, bool continueOnCapturedContext, CancellationToken cancellationToken, Message message, IAmABoxTransactionConnectionProvider overridingTransactionConnectionProvider = null)
            where T : class, IRequest
        {
            var written = await RetryAsync(async ct => { await AsyncOutbox.AddAsync(message, OutboxTimeout, ct, overridingTransactionConnectionProvider).ConfigureAwait(continueOnCapturedContext); },
                    continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
        }            
            
            
        internal void AddToOutbox<T>(T request, Message message, IAmABoxTransactionConnectionProvider overridingTransactionConnectionProvider = null) where T : class, IRequest
        {
            var written = Retry(() => { OutBox.Add(message, OutboxTimeout, overridingTransactionConnectionProvider); });

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
        }

        internal void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (OutBox != null || AsyncOutbox != null);
            if (!hasOutBox)
                return;

            int maxOutStandingMessages = -1;
            if (MessageProducerSync != null)
                maxOutStandingMessages = MessageProducerSync.MaxOutStandingMessages;

            if (AsyncMessageProducer != null)
                maxOutStandingMessages = AsyncMessageProducer.MaxOutStandingMessages;

            s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit = maxOutStandingMessages != -1 && _outStandingCount > maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException($"The outbox limit of {maxOutStandingMessages} has been exceeded");
        }

        internal void CheckOutstandingMessages()
        {
            if (MessageProducerSync == null)
                return;

            var now = DateTime.UtcNow;
            var checkInterval = TimeSpan.FromMilliseconds(MessageProducerSync.MaxOutStandingCheckIntervalMilliSeconds);


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

        internal void ClearOutbox(params Guid[] posts)
        {
            if (!HasOutbox())
                throw new InvalidOperationException("No outbox defined.");
            if (MessageProducerSync == null)
                throw new InvalidOperationException("No message producer defined.");

            CheckOutboxOutstandingLimit();

            foreach (var messageId in posts)
            {
                var message = OutBox.Get(messageId);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, messageId.ToString());

                if (MessageProducerSync is ISupportPublishConfirmation producer)
                {
                    //mark dispatch handled by a callback - set in constructor
                    Retry(() => { MessageProducerSync.Send(message); });
                }
                else
                {
                    var sent = Retry(() => { MessageProducerSync.Send(message); });
                    if (sent)
                        Retry(() => OutBox.MarkDispatched(messageId, DateTime.UtcNow));
                }
            }
            
            CheckOutstandingMessages();

        }

        internal async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {

            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");
            if (AsyncMessageProducer == null)
                throw new InvalidOperationException("No async message producer defined.");

            CheckOutboxOutstandingLimit();

            foreach (var messageId in posts)
            {
                var message = await AsyncOutbox.GetAsync(messageId, OutboxTimeout, cancellationToken);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, messageId.ToString());

                if (MessageProducerSync is ISupportPublishConfirmation producer)
                {
                    //mark dispatch handled by a callback - set in constructor
                    await RetryAsync(
                            async ct =>
                                await AsyncMessageProducer.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                            continueOnCapturedContext,
                            cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext);
                }
                else
                {

                    var sent = await RetryAsync(
                            async ct =>
                                await AsyncMessageProducer.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                            continueOnCapturedContext,
                            cancellationToken)
                        .ConfigureAwait(continueOnCapturedContext);

                    if (sent)
                        await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(messageId, DateTime.UtcNow));
                }
            }
            
            CheckOutstandingMessages();

        }

        internal bool ConfigureAsyncPublisherCallbackMaybe()
        {
            if (AsyncMessageProducer == null)
                return false;

            if (AsyncMessageProducer is ISupportPublishConfirmation producer)
            {
                producer.OnMessagePublished += async delegate(bool success, Guid id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id.ToString());
                        if (AsyncOutbox != null)
                            await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(id, DateTime.UtcNow));
                    }
                };
                return true;
            }

            return false;
        }

        internal bool ConfigurePublisherCallbackMaybe()
        {
            if (MessageProducerSync is ISupportPublishConfirmation producer)
            {
                producer.OnMessagePublished += delegate(bool success, Guid id)
                {
                    if (success)
                    {
                        s_logger.LogInformation("Sent message: Id:{Id}", id.ToString());
                        if (OutBox != null)
                            Retry(() => OutBox.MarkDispatched(id, DateTime.UtcNow));
                    }
                };
                return true;
            }

            return false;
        }

        internal bool HasAsyncOutbox()
        {
            return AsyncOutbox != null;
        }

        internal bool HasOutbox()
        {
            return OutBox != null;
        }

        internal void OutstandingMessagesCheck()
        {
            if (Monitor.TryEnter(_checkOutStandingMessagesObject))
            {

                s_logger.LogDebug("Begin count of outstanding messages");
                try
                {
                    if ((OutBox != null) && (OutBox is IAmAnOutboxViewer<Message> outboxViewer))
                    {
                        _outStandingCount = outboxViewer
                            .OutstandingMessages(MessageProducerSync.MaxOutStandingCheckIntervalMilliSeconds)
                            .Count();
                        return;
                    }

                    //TODO: There is no async version of this call at present; the thread here means that won't hurt if implemented
                    if ((AsyncOutbox != null) && (AsyncOutbox is IAmAnOutboxViewer<Message> asyncOutboxViewer))
                    {
                        _outStandingCount = asyncOutboxViewer
                            .OutstandingMessages(MessageProducerSync.MaxOutStandingCheckIntervalMilliSeconds)
                            .Count();
                        return;
                    }

                    if ((OutBox == null) && (AsyncOutbox == null))
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
            var policy = PolicyRegistry.Get<Policy>(CommandProcessor.RETRYPOLICY);
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
            var result = await PolicyRegistry.Get<AsyncPolicy>(CommandProcessor.RETRYPOLICYASYNC)
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

        internal void SendViaExternalBus<T, TResponse>(Message outMessage) where T : class, ICall where TResponse : class, IResponse
        {
            Retry(() => MessageProducerSync.Send(outMessage));
        }
        
    }
}
