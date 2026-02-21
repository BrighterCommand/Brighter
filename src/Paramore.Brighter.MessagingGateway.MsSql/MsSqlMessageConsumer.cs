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
        private readonly IAmAMessageScheduler? _scheduler;
        private MsSqlMessageProducer? _requeueProducer;
        private bool _requeueProducerInitialized;
        private object? _requeueProducerLock;

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            string topic,
            RelationalDbConnectionProvider connectionProvider,
            IAmAMessageScheduler? scheduler = null
            )
        {
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _msSqlConfiguration = msSqlConfiguration;
            _sqlMessageQueue = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectionProvider);
            _scheduler = scheduler;
        }

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            string topic,
            IAmAMessageScheduler? scheduler = null)
            : this(msSqlConfiguration, topic, new MsSqlConnectionProvider(msSqlConfiguration), scheduler)
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
        /// Rejects the specified message.
        /// </summary>
        /// <remarks>
        ///  Not implemented for the MSSQL message consumer
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        public bool Reject(Message message, MessageRejectionReason? reason = null)
         {
             Log.RejectingMessageNotImplemented(s_logger, message.Header.Topic, message.Id);
             return false;
         }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <remarks>
        ///  Not implemented for the MSSQL message consumer
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="reason">The <see cref="MessageRejectionReason"/> that explains why we rejected the message</param>
        /// /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the reject</param>
        public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null, CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(Reject(message, reason));

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

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "MsSqlMessagingConsumer: purging queue")]
            public static partial void PurgingQueue(ILogger logger);

            [LoggerMessage(LogLevel.Information, "MsSqlMessagingConsumer: rejecting message with topic {Topic} and id {Id}, NOT IMPLEMENTED")]
            public static partial void RejectingMessageNotImplemented(ILogger logger, string topic, string id);
            
            [LoggerMessage(LogLevel.Debug, "MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}")]
            public static partial void RequeuingMessage(ILogger logger, string topic, string id);
        }
    }
}

