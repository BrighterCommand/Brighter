using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        internal int OutboxBulkChunkSize { get; set; } = 100;

        internal IAmAProducerRegistry ProducerRegistry { get; set; }

        private static readonly SemaphoreSlim _backgroundClearSemaphoreToken = new SemaphoreSlim(1, 1);
        //Used to checking the limit on outstanding messages for an Outbox. We throw at that point. Writes to the static bool should be made thread-safe by locking the object
        private static readonly SemaphoreSlim _checkOutstandingSemaphoreToken = new SemaphoreSlim(1, 1);
        
        private const string ADDMESSAGETOOUTBOX = "Add message to outbox";
        private const string GETMESSAGESFROMOUTBOX = "Get outstanding messages from the outbox";
        private const string DISPATCHMESSAGE = "Dispatching message";
        private const string BULKDISPATCHMESSAGE = "Bulk dispatching messages";

        private DateTime _lastOutStandingMessageCheckAt = DateTime.UtcNow;
        
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
            CheckOutboxOutstandingLimit();
                
            var written = await RetryAsync(async ct => { await AsyncOutbox.AddAsync(message, OutboxTimeout, ct, overridingTransactionConnectionProvider).ConfigureAwait(continueOnCapturedContext); },
                    continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
            Activity.Current?.AddEvent(new ActivityEvent(ADDMESSAGETOOUTBOX,
                tags: new ActivityTagsCollection {{"MessageId", message.Id}}));
        }

        internal async Task AddToOutboxAsync(IEnumerable<Message> messages, bool continueOnCapturedContext, CancellationToken cancellationToken, IAmABoxTransactionConnectionProvider overridingTransactionConnectionProvider = null)
        {
            CheckOutboxOutstandingLimit();

#pragma warning disable CS0618
            if (AsyncOutbox is IAmABulkOutboxAsync<Message> box)
#pragma warning restore CS0618
            {
                foreach (var chunk in ChunkMessages(messages))
                {
                    var written = await RetryAsync(
                        async ct =>
                        {
                            await box.AddAsync(chunk, OutboxTimeout, ct, overridingTransactionConnectionProvider)
                                .ConfigureAwait(continueOnCapturedContext);
                        },
                        continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);

                    if (!written)
                        throw new ChannelFailureException($"Could not write {chunk.Count()} requests to the outbox");
                }
            }
            else
            {
                throw new InvalidOperationException($"{AsyncOutbox.GetType()} does not implement IAmABulkOutboxAsync");
            }
        } 
            
        internal void AddToOutbox<T>(T request, Message message, IAmABoxTransactionConnectionProvider overridingTransactionConnectionProvider = null) where T : class, IRequest
        {
            CheckOutboxOutstandingLimit();
                
            var written = Retry(() => { OutBox.Add(message, OutboxTimeout, overridingTransactionConnectionProvider); });

            if (!written)
                throw new ChannelFailureException($"Could not write request {request.Id} to the outbox");
            Activity.Current?.AddEvent(new ActivityEvent(ADDMESSAGETOOUTBOX,
                tags: new ActivityTagsCollection {{"MessageId", message.Id}}));
        }
        
        internal void AddToOutbox(IEnumerable<Message> messages, IAmABoxTransactionConnectionProvider overridingTransactionConnectionProvider = null) 
        {
            CheckOutboxOutstandingLimit();

#pragma warning disable CS0618
            if (OutBox is IAmABulkOutboxSync<Message> box)
#pragma warning restore CS0618
            {
                foreach (var chunk in ChunkMessages(messages))
                {
                    var written =
                        Retry(() => { box.Add(chunk, OutboxTimeout, overridingTransactionConnectionProvider); });

                    if (!written)
                        throw new ChannelFailureException($"Could not write {chunk.Count()} messages to the outbox");
                }
            }
            else
            {
                throw new InvalidOperationException($"{OutBox.GetType()} does not implement IAmABulkOutboxSync");
            }
        }

        private IEnumerable<List<Message>> ChunkMessages(IEnumerable<Message> messages)
        {
            return Enumerable.Range(0, (int)Math.Ceiling((messages.Count() / (decimal)OutboxBulkChunkSize)))
                .Select(i => new List<Message>(messages
                    .Skip(i * OutboxBulkChunkSize)
                    .Take(OutboxBulkChunkSize)
                    .ToArray()));
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
        }

        internal void ClearOutbox(params Guid[] posts)
        {
            if (!HasOutbox())
                throw new InvalidOperationException("No outbox defined.");

            foreach (var messageId in posts)
            {
                var message = OutBox.Get(messageId);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                Dispatch(new[] {message});
            }
            
            CheckOutstandingMessages();
        }

        internal async Task ClearOutboxAsync(
            IEnumerable<Guid> posts, 
            bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
        {

            if (!HasAsyncOutbox())
                throw new InvalidOperationException("No async outbox defined.");

            foreach (var messageId in posts)
            {
                var message = await AsyncOutbox.GetAsync(messageId, OutboxTimeout, cancellationToken);
                if (message == null || message.Header.MessageType == MessageType.MT_NONE)
                    throw new NullReferenceException($"Message with Id {messageId} not found in the Outbox");

                await DispatchAsync(new[] {message}, continueOnCapturedContext, cancellationToken);
            }

            CheckOutstandingMessages();
        }

        /// <summary>
        /// This is the clear outbox for implicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useAsync">Use the Async outbox and Producer</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
         internal void ClearOutbox(int amountToClear, int minimumAge, bool useAsync, bool useBulk, Dictionary<string, object> args = null)
        {
            var span = Activity.Current;
            span?.AddTag("amountToClear", amountToClear);
            span?.AddTag("minimumAge", minimumAge);
            span?.AddTag("async", useAsync);
            span?.AddTag("bulk", useBulk);
            
            if (useAsync)
            {
                if (!HasAsyncOutbox())
                    throw new InvalidOperationException("No async outbox defined.");
                
                Task.Run(() => BackgroundDispatchUsingAsync(amountToClear, minimumAge, useBulk, args), CancellationToken.None);
            }

            else
            {
                if (!HasOutbox())
                    throw new InvalidOperationException("No outbox defined.");
                
                Task.Run(() => BackgroundDispatchUsingSync(amountToClear, minimumAge, args));
            }
        }
        
        /// <summary>
        /// This is the clear outbox for implicit clearing of messages.
        /// </summary>
        /// <param name="amountToClear">Maximum number to clear.</param>
        /// <param name="minimumAge">The minimum age of messages to be cleared in milliseconds.</param>
        /// <param name="useAsync">Use the Async outbox and Producer</param>
        /// <param name="useBulk">Use bulk sending capability of the message producer, this must be paired with useAsync.</param>
        /// <param name="args">Optional bag of arguments required by an outbox implementation to sweep</param>
        internal Task ClearOutboxAsync(int amountToClear, int minimumAge, bool useAsync, bool useBulk, Dictionary<string, object> args = null)
        {
            var span = Activity.Current;
            span?.AddTag("amountToClear", amountToClear);
            span?.AddTag("minimumAge", minimumAge);
            span?.AddTag("async", useAsync);
            span?.AddTag("bulk", useBulk);
            
            if (useAsync)
            {
                if (!HasAsyncOutbox())
                    throw new InvalidOperationException("No async outbox defined.");
                
                return BackgroundDispatchUsingAsync(amountToClear, minimumAge, useBulk, args);
            }

            else
            {
                if (!HasOutbox())
                    throw new InvalidOperationException("No outbox defined.");
                
                return BackgroundDispatchUsingSync(amountToClear, minimumAge, args);
            }
        }

        private async Task BackgroundDispatchUsingSync(int amountToClear, int minimumAge, Dictionary<string, object> args)
        {
            var span = Activity.Current;
            if (await _backgroundClearSemaphoreToken.WaitAsync(TimeSpan.Zero))
            {
                try
                {
                    
                    var messages = OutBox.OutstandingMessages(minimumAge, amountToClear, args:args);
                    span?.AddEvent(new ActivityEvent(GETMESSAGESFROMOUTBOX,
                        tags: new ActivityTagsCollection {{"Outstanding Messages", messages.Count()}}));
                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);
                    Dispatch(messages);
                    s_logger.LogInformation("Messages have been cleared");
                    span?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                    s_logger.LogError(e, "Error while dispatching from outbox");
                }
                finally
                {
                    span?.Dispose();
                    _backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages();
            }
            else
            {
                span?.SetStatus(ActivityStatusCode.Error)
                    .Dispose();
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }
        
        private async Task BackgroundDispatchUsingAsync(int amountToClear, int minimumAge, bool useBulk, Dictionary<string, object> args)
        {
            var span = Activity.Current;
            if (await _backgroundClearSemaphoreToken.WaitAsync(TimeSpan.Zero))
            {
                try
                {
                    
                    var messages =
                        await AsyncOutbox.OutstandingMessagesAsync(minimumAge, amountToClear, args: args);
                    span?.AddEvent(new ActivityEvent(GETMESSAGESFROMOUTBOX));

                    s_logger.LogInformation("Found {NumberOfMessages} to clear out of amount {AmountToClear}",
                        messages.Count(), amountToClear);

                    if (useBulk)
                    {
                        await BulkDispatchAsync(messages, CancellationToken.None);
                    }
                    else
                    {
                        await DispatchAsync(messages, false, CancellationToken.None);
                    }

                    span?.SetStatus(ActivityStatusCode.Ok);
                    s_logger.LogInformation("Messages have been cleared");
                }
                catch (Exception e)
                {
                    s_logger.LogError(e, "Error while dispatching from outbox");
                    span?.SetStatus(ActivityStatusCode.Error, "Error while dispatching from outbox");
                }
                finally
                {
                    span?.Dispose();
                    _backgroundClearSemaphoreToken.Release();
                }

                CheckOutstandingMessages();
            }
            else
            {
                span?.SetStatus(ActivityStatusCode.Error)
                    .Dispose();
                s_logger.LogInformation("Skipping dispatch of messages as another thread is running");
            }
        }

        private void Dispatch(IEnumerable<Message> posts)
        {
            foreach (var message in posts)
            {
                Activity.Current?.AddEvent(new ActivityEvent(DISPATCHMESSAGE,
                    tags: new ActivityTagsCollection {{"Topic", message.Header.Topic}, {"MessageId", message.Id}}));
                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, message.Id.ToString());

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
                            Retry(() => OutBox.MarkDispatched(message.Id, DateTime.UtcNow));
                    }
                }
                else
                    throw new InvalidOperationException("No sync message producer defined.");
            }
        }
        
        private async Task DispatchAsync(IEnumerable<Message> posts, bool continueOnCapturedContext, CancellationToken cancellationToken)
        {
            foreach (var message in posts)
            {
                Activity.Current?.AddEvent(new ActivityEvent(DISPATCHMESSAGE,
                    tags: new ActivityTagsCollection {{"Topic", message.Header.Topic}, {"MessageId", message.Id}}));
                s_logger.LogInformation("Decoupled invocation of message: Topic:{Topic} Id:{Id}", message.Header.Topic, message.Id.ToString());
                
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
                            await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(message.Id, DateTime.UtcNow, cancellationToken: cancellationToken), 
                                cancellationToken: cancellationToken);
                    }
                }
                else
                    throw new InvalidOperationException("No async message producer defined.");
            }
        }
        
        private async Task BulkDispatchAsync(IEnumerable<Message> posts, CancellationToken cancellationToken)
        {
            var span = Activity.Current;
            //Chunk into Topics
            var messagesByTopic = posts.GroupBy(m => m.Header.Topic);

            foreach (var topicBatch in messagesByTopic)
            {
                var producer = ProducerRegistry.LookupByOrDefault(topicBatch.Key);

                if (producer is IAmABulkMessageProducerAsync bulkMessageProducer)
                {
                    var messages = topicBatch.ToArray();
                    s_logger.LogInformation("Bulk Dispatching {NumberOfMessages} for Topic {TopicName}", messages.Length, topicBatch.Key);
                    span?.AddEvent(new ActivityEvent(BULKDISPATCHMESSAGE,
                        tags: new ActivityTagsCollection {{"Topic", topicBatch.Key}, {"Number Of Messages", messages.Length}}));
                    var dispatchesMessages = bulkMessageProducer.SendAsync(messages, cancellationToken);

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
                            await RetryAsync(async ct => await AsyncOutbox.MarkDispatchedAsync(id, DateTime.UtcNow, cancellationToken: ct));
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
        internal bool HasAsyncBulkOutbox()
        {
#pragma warning disable CS0618
            return AsyncOutbox is IAmABulkOutboxAsync<Message>;
#pragma warning restore CS0618
        }

        internal bool HasOutbox()
        {
            return OutBox != null;
        }
        
        internal bool HasBulkOutbox()
        {
#pragma warning disable CS0618
            return OutBox is IAmABulkOutboxSync<Message>;
#pragma warning restore CS0618
        }

        private void OutstandingMessagesCheck()
        {
            _checkOutstandingSemaphoreToken.Wait();
            
            _lastOutStandingMessageCheckAt = DateTime.UtcNow;
            s_logger.LogDebug("Begin count of outstanding messages");
            try
            {
                var producer = ProducerRegistry.GetDefaultProducer();
                if (OutBox != null)
                {
                    _outStandingCount = OutBox
                        .OutstandingMessages(
                            producer.MaxOutStandingCheckIntervalMilliSeconds,
                            args:producer.OutBoxBag 
                            )
                        .Count();
                    return;
                }
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
                _checkOutstandingSemaphoreToken.Release();
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

        private async Task<bool> RetryAsync(Func<CancellationToken, Task> send, bool continueOnCapturedContext = false,
            CancellationToken cancellationToken = default)
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
