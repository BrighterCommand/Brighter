﻿using System;
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

        internal IAmAProducerRegistry ProducerRegistry { get; set; }

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

            if (disposing && ProducerRegistry != null)
                ProducerRegistry.CloseAll();
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

        private void CheckOutboxOutstandingLimit()
        {
            bool hasOutBox = (OutBox != null || AsyncOutbox != null);
            if (!hasOutBox)
                return;

            int maxOutStandingMessages = ProducerRegistry.GetDefaultProducer().MaxOutStandingMessages;

            s_logger.LogDebug("Outbox outstanding message count is: {OutstandingMessageCount}", _outStandingCount);
            // Because a thread recalculates this, we may always be in a delay, so we check on entry for the next outstanding item
            bool exceedsOutstandingMessageLimit = maxOutStandingMessages != -1 && _outStandingCount > maxOutStandingMessages;

            if (exceedsOutstandingMessageLimit)
                throw new OutboxLimitReachedException($"The outbox limit of {maxOutStandingMessages} has been exceeded");
        }

        private void CheckOutstandingMessages()
        {
            var now = DateTime.UtcNow;
            var checkInterval = TimeSpan.FromMilliseconds(ProducerRegistry.GetDefaultProducer().MaxOutStandingCheckIntervalMilliSeconds);


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

            CheckOutboxOutstandingLimit();

            foreach (var messageId in posts)
            {
                var message = OutBox.Get(messageId);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, messageId.ToString());

                var producer = ProducerRegistry.LookupByOrDefault(message.Header.Topic);

                if (producer is IAmAMessageProducerSync producerSync)
                {
                    if (producer is ISupportPublishConfirmation)
                    {
                        //mark dispatch handled by a callback - set in constructor
                        Retry(() => { producerSync.Send(message); });
                    }
                    else
                    {
                        var sent = Retry(() => { producerSync.Send(message); });
                        if (sent)
                            Retry(() => OutBox.MarkDispatched(messageId, DateTime.UtcNow));
                    }
                }
                else
                    throw new InvalidOperationException("No sync message producer defined.");
                   
            }
            
            CheckOutstandingMessages();

        }

        internal async Task ClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {

            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            CheckOutboxOutstandingLimit();

            foreach (var messageId in posts)
            {
                var message = await AsyncOutbox.GetAsync(messageId, OutboxTimeout, cancellationToken);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, messageId.ToString());
                
                var producer = ProducerRegistry.LookupByOrDefault(message.Header.Topic);

                if (producer is IAmAMessageProducerAsync producerAsync)
                {
                    if (producer is ISupportPublishConfirmation)
                    {
                        //mark dispatch handled by a callback - set in constructor
                        await RetryAsync(
                                async ct =>
                                    await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                continueOnCapturedContext,
                                cancellationToken)
                            .ConfigureAwait(continueOnCapturedContext);
                    }
                    else
                    {

                        var sent = await RetryAsync(
                                async ct =>
                                    await producerAsync.SendAsync(message).ConfigureAwait(continueOnCapturedContext),
                                continueOnCapturedContext,
                                cancellationToken)
                            .ConfigureAwait(continueOnCapturedContext);

                        if (sent)
                            await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(messageId, DateTime.UtcNow, cancellationToken: cancellationToken), 
                                cancellationToken: cancellationToken);
                    }
                }
                else
                    throw new InvalidOperationException("No async message producer defined.");
            }
            
            CheckOutstandingMessages();

        }

        internal async Task BulkClearOutboxAsync(IEnumerable<Guid> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            var messages = await AsyncOutbox.GetAsync(posts, OutboxTimeout, cancellationToken);

            s_logger.LogInformation(
                "{RequestedNumberOfMessages} of {RetrievedNumberOfMessages} retrieved from the Outbox.", posts.Count(),
                messages.Count());

            await BulkClearOutboxAsync(messages, continueOnCapturedContext, cancellationToken);
        }

        internal async Task BulkClearOutboxAsync(IEnumerable<Message> posts, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            CheckOutboxOutstandingLimit();

            //Chunk into Topics
            var messagesByTopic = posts.GroupBy(m => m.Header.Topic);

            foreach (var topicBatch in messagesByTopic)
            {
                var producer = ProducerRegistry.LookupBy(topicBatch.Key);

                if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                {
                    var messages = topicBatch.ToArray();
                    s_logger.LogInformation("Bulk Dispatching {NumberOfMessages} for Topic {TopicName}", messages.Length, topicBatch.Key);
                    var dispatchesMessages = bulkMessageProducer.SendAsync(messages, 10, cancellationToken);

                    await foreach (var successfulMessage in dispatchesMessages.WithCancellation(cancellationToken))
                    {
                        if (!(producer is ISupportPublishConfirmation))
                        {
                            await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(successfulMessage,
                                DateTime.UtcNow, cancellationToken: cancellationToken), cancellationToken: cancellationToken);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("No async bulk message producer defined.");
                }
            }

            CheckOutstandingMessages();
        }

        internal bool ConfigureAsyncPublisherCallbackMaybe(IAmAMessageProducer producer)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += async delegate(bool success, Guid id)
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

        internal bool ConfigurePublisherCallbackMaybe(IAmAMessageProducer producer)
        {
            if (producer is ISupportPublishConfirmation producerSync)
            {
                producerSync.OnMessagePublished += delegate(bool success, Guid id)
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
                            .OutstandingMessages(ProducerRegistry.GetDefaultProducer().MaxOutStandingCheckIntervalMilliSeconds)
                            .Count();
                        return;
                    }

                    //TODO: There is no async version of this call at present; the thread here means that won't hurt if implemented
                    if ((AsyncOutbox != null) && (AsyncOutbox is IAmAnOutboxViewer<Message> asyncOutboxViewer))
                    {
                        _outStandingCount = asyncOutboxViewer
                            .OutstandingMessages(ProducerRegistry.GetDefaultProducer().MaxOutStandingCheckIntervalMilliSeconds)
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

        internal void CallViaExternalBus<T, TResponse>(Message outMessage) where T : class, ICall where TResponse : class, IResponse
        {
            //We assume that this only occurs over a blocking producer
            var producer = ProducerRegistry.LookupByOrDefault(outMessage.Header.Topic);
        if (producer is IAmAMessageProducerSync producerSync)
                Retry(() => producerSync.Send(outMessage));
        }
        
    }
}
