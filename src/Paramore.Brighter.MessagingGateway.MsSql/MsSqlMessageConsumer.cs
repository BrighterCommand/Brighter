using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public partial class MsSqlMessageConsumer : IAmAMessageConsumerSync, IAmAMessageConsumerAsync
    {
        private readonly string _topic;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageConsumer>();
        private readonly MsSqlMessageQueue<Message> _sqlMessageQueue;
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;
        private readonly RoutingKey? _deadLetterRoutingKey;
        private readonly RoutingKey? _invalidMessageRoutingKey;
        private readonly Lazy<MsSqlMessageProducer?>? _deadLetterProducer;
        private readonly Lazy<MsSqlMessageProducer?>? _invalidMessageProducer;
        private readonly IAmAMessageScheduler? _scheduler;
        private MsSqlMessageProducer? _requeueProducer;
        private bool _requeueProducerInitialized;
        private object? _requeueProducerLock;

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            string topic,
            RelationalDbConnectionProvider connectionProvider,
            IAmAMessageScheduler? scheduler = null,
            RoutingKey? deadLetterRoutingKey = null,
            RoutingKey? invalidMessageRoutingKey = null)
        {
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _msSqlConfiguration = msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            _sqlMessageQueue = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectionProvider);
            _scheduler = scheduler;
            _deadLetterRoutingKey = deadLetterRoutingKey;
            _invalidMessageRoutingKey = invalidMessageRoutingKey;

            // LazyThreadSafetyMode.None: message pumps are single-threaded per consumer, so no
            // thread-safety mode is needed. None does not cache exceptions, allowing the factory
            // to retry on the next .Value access after a transient failure.
            if (_deadLetterRoutingKey != null)
                _deadLetterProducer = new Lazy<MsSqlMessageProducer?>(CreateDeadLetterProducer, LazyThreadSafetyMode.None);
            if (_invalidMessageRoutingKey != null)
                _invalidMessageProducer = new Lazy<MsSqlMessageProducer?>(CreateInvalidMessageProducer, LazyThreadSafetyMode.None);
        }

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            string topic,
            IAmAMessageScheduler? scheduler = null,
            RoutingKey? deadLetterRoutingKey = null,
            RoutingKey? invalidMessageRoutingKey = null)
            : this(msSqlConfiguration, topic, new MsSqlConnectionProvider(msSqlConfiguration), scheduler, deadLetterRoutingKey, invalidMessageRoutingKey)
        {}

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <remarks>
        /// No implementation required because of atomic 'read-and-delete'
        /// </remarks>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message) {}

        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            Log.PurgingQueue(s_logger);
            _sqlMessageQueue.Purge();
        }

        public async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.PurgingQueue(s_logger);
            await Task.Run( () => _sqlMessageQueue.Purge(), cancellationToken);
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeOut">How long to wait on a recieve. Default is 300ms</param>
        /// <returns>Message</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            timeOut ??= TimeSpan.FromMilliseconds(300);

            var rc = _sqlMessageQueue.TryReceive(_topic, timeOut.Value);
            var message = !rc.IsDataValid ? new Message() : rc.Message;
            return [message!];
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeOut">How long to wait on a recieve. Default is 300ms</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
        /// <returns>Message</returns>
        public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ct = new CancellationTokenSource(timeOut ?? TimeSpan.FromMilliseconds(300));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct.Token);

            var rc = await _sqlMessageQueue.TryReceiveAsync(_topic, linked.Token);
            var message = !rc.IsDataValid ? new Message() : rc.Message;
            return [message!];
        }

        /// <summary>
        /// Rejects the specified message, routing it to a DLQ or invalid message channel if configured.
        /// </summary>
        /// <remarks>
        /// MsSql uses atomic read-and-delete, so the message has already been removed from the source queue
        /// on <see cref="Receive"/>. Reject only needs to forward the message to the appropriate rejection channel.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        public bool Reject(Message message, MessageRejectionReason? reason = null)
        {
            if (_deadLetterProducer == null && _invalidMessageProducer == null)
            {
                if (reason != null)
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());

                return true;
            }

            var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

            try
            {
                RefreshMetadata(message, reason);

                var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                    rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

                MsSqlMessageProducer? producer = null;
                if (shouldRoute)
                {
                    message.Header.Topic = routingKey!;
                    if (isFallingBackToDlq)
                        Log.FallingBackToDlq(s_logger, message.Id);

                    if (routingKey == _invalidMessageRoutingKey)
                        producer = _invalidMessageProducer?.Value;
                    else if (routingKey == _deadLetterRoutingKey)
                        producer = _deadLetterProducer?.Value;
                }

                if (producer != null)
                {
                    producer.Send(message);
                    Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
                }
                else
                {
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
                }
            }
            catch (Exception ex)
            {
                // DLQ send failed — the message was already atomically deleted from the source
                // queue on Receive, so we cannot requeue it. Log and return true to prevent the
                // message pump from retrying endlessly.
                Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
                return true;
            }

            return true;
        }

        /// <summary>
        /// Rejects the specified message asynchronously, routing it to a DLQ or invalid message channel if configured.
        /// </summary>
        /// <remarks>
        /// MsSql uses atomic read-and-delete, so the message has already been removed from the source queue
        /// on <see cref="ReceiveAsync"/>. Reject only needs to forward the message to the appropriate rejection channel.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the reject</param>
        public async Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (_deadLetterProducer == null && _invalidMessageProducer == null)
            {
                if (reason != null)
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Id, reason.RejectionReason.ToString());

                return true;
            }

            var rejectionReason = reason?.RejectionReason ?? RejectionReason.None;

            try
            {
                RefreshMetadata(message, reason);

                var (routingKey, shouldRoute, isFallingBackToDlq) = DetermineRejectionRoute(
                    rejectionReason, _invalidMessageProducer != null, _deadLetterProducer != null);

                MsSqlMessageProducer? producer = null;
                if (shouldRoute)
                {
                    message.Header.Topic = routingKey!;
                    if (isFallingBackToDlq)
                        Log.FallingBackToDlq(s_logger, message.Id);

                    if (routingKey == _invalidMessageRoutingKey)
                        producer = _invalidMessageProducer?.Value;
                    else if (routingKey == _deadLetterRoutingKey)
                        producer = _deadLetterProducer?.Value;
                }

                if (producer != null)
                {
                    await producer.SendAsync(message, cancellationToken);
                    Log.MessageSentToRejectionChannel(s_logger, message.Id, rejectionReason.ToString());
                }
                else
                {
                    Log.NoChannelsConfiguredForRejection(s_logger, message.Id, rejectionReason.ToString());
                }
            }
            catch (Exception ex)
            {
                // DLQ send failed — the message was already atomically deleted from the source
                // queue on ReceiveAsync, so we cannot requeue it. Log and return true to prevent
                // the message pump from retrying endlessly.
                Log.ErrorSendingToRejectionChannel(s_logger, ex, message.Id, rejectionReason.ToString());
                return true;
            }

            return true;
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">When greater than zero, uses a producer with scheduler for delayed requeue</param>
        /// <returns>True when message is requeued</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;

            var topic = message.Header.Topic;
            Log.RequeuingMessage(s_logger, topic, message.Id.ToString());

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                _requeueProducer!.SendWithDelay(message, delay);
                return true;
            }

            _sqlMessageQueue.Send(message, topic);
            return true;
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">When greater than zero, uses a producer with scheduler for delayed requeue</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
        /// <returns>True when message is requeued</returns>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            delay ??= TimeSpan.Zero;

            var topic = message.Header.Topic;
            Log.RequeuingMessage(s_logger, topic, message.Id.ToString());

            if (delay > TimeSpan.Zero)
            {
                EnsureRequeueProducer();
                await _requeueProducer!.SendWithDelayAsync(message, delay, cancellationToken);
                return true;
            }

            await _sqlMessageQueue.SendAsync(message, topic, null, cancellationToken: cancellationToken);
            return true;
        }

        /// <summary>
        /// Dispose of the consumer
        /// </summary>
        public void Dispose()
        {
            _requeueProducer?.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            if (_requeueProducer != null) await _requeueProducer.DisposeAsync();
            GC.SuppressFinalize(this);
        }

        private void EnsureRequeueProducer()
        {
            LazyInitializer.EnsureInitialized(ref _requeueProducer, ref _requeueProducerInitialized,
                ref _requeueProducerLock, () => new MsSqlMessageProducer(_msSqlConfiguration)
                {
                    Scheduler = _scheduler
                });
        }

        private MsSqlMessageProducer? CreateDeadLetterProducer()
        {
            if (_deadLetterRoutingKey == null) return null;

            try
            {
                return new MsSqlMessageProducer(_msSqlConfiguration,
                    new Publication { Topic = _deadLetterRoutingKey });
            }
            catch (Exception e)
            {
                Log.ErrorCreatingDlqProducer(s_logger, e, _deadLetterRoutingKey.Value);
                return null;
            }
        }

        private MsSqlMessageProducer? CreateInvalidMessageProducer()
        {
            if (_invalidMessageRoutingKey == null) return null;

            try
            {
                return new MsSqlMessageProducer(_msSqlConfiguration,
                    new Publication { Topic = _invalidMessageRoutingKey });
            }
            catch (Exception e)
            {
                Log.ErrorCreatingInvalidMessageProducer(s_logger, e, _invalidMessageRoutingKey.Value);
                return null;
            }
        }

        private static void RefreshMetadata(Message message, MessageRejectionReason? reason)
        {
            message.Header.Bag["originalTopic"] = message.Header.Topic.Value;
            message.Header.Bag["rejectionTimestamp"] = DateTimeOffset.UtcNow.ToString("o");
            message.Header.Bag["originalMessageType"] = message.Header.MessageType.ToString();

            if (reason == null) return;

            message.Header.Bag["rejectionReason"] = reason.RejectionReason.ToString();
            if (!string.IsNullOrEmpty(reason.Description))
                message.Header.Bag["rejectionMessage"] = reason.Description ?? string.Empty;
        }

        private (RoutingKey? routingKey, bool foundProducer, bool isFallingBackToDlq) DetermineRejectionRoute(
            RejectionReason rejectionReason,
            bool hasInvalidProducer,
            bool hasDeadLetterProducer)
        {
            switch (rejectionReason)
            {
                case RejectionReason.Unacceptable:
                    if (hasInvalidProducer)
                        return (_invalidMessageRoutingKey, true, false);
                    if (hasDeadLetterProducer)
                        return (_deadLetterRoutingKey, true, true);
                    return (null, false, false);

                case RejectionReason.DeliveryError:
                case RejectionReason.None:
                default:
                    if (hasDeadLetterProducer)
                        return (_deadLetterRoutingKey, true, false);
                    return (null, false, false);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "MsSqlMessagingConsumer: purging queue")]
            public static partial void PurgingQueue(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}")]
            public static partial void RequeuingMessage(ILogger logger, string topic, string id);

            [LoggerMessage(LogLevel.Warning, "MsSqlMessagingConsumer: No DLQ or invalid message channels configured for message {MessageId}, rejection reason: {RejectionReason}")]
            public static partial void NoChannelsConfiguredForRejection(ILogger logger, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Information, "MsSqlMessagingConsumer: Message {MessageId} sent to rejection channel, reason: {RejectionReason}")]
            public static partial void MessageSentToRejectionChannel(ILogger logger, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Warning, "MsSqlMessagingConsumer: Falling back to DLQ for message {MessageId}")]
            public static partial void FallingBackToDlq(ILogger logger, string messageId);

            [LoggerMessage(LogLevel.Error, "MsSqlMessagingConsumer: Error sending message {MessageId} to rejection channel, reason: {RejectionReason}")]
            public static partial void ErrorSendingToRejectionChannel(ILogger logger, Exception ex, string messageId, string rejectionReason);

            [LoggerMessage(LogLevel.Error, "MsSqlMessagingConsumer: Error creating DLQ producer for routing key {RoutingKey}")]
            public static partial void ErrorCreatingDlqProducer(ILogger logger, Exception ex, string routingKey);

            [LoggerMessage(LogLevel.Error, "MsSqlMessagingConsumer: Error creating invalid message producer for routing key {RoutingKey}")]
            public static partial void ErrorCreatingInvalidMessageProducer(ILogger logger, Exception ex, string routingKey);
        }
    }
}
