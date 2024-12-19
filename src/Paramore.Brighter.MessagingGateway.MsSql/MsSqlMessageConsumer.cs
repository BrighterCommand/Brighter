﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageConsumer : IAmAMessageConsumer, IAmAMessageConsumerAsync
    {
        private readonly string _topic;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageConsumer>();
        private readonly MsSqlMessageQueue<Message> _sqlMessageQueue;

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration, 
            string topic, 
            RelationalDbConnectionProvider connectionProvider
            )
        {
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _sqlMessageQueue = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectionProvider);
        }

        public MsSqlMessageConsumer(RelationalDatabaseConfiguration msSqlConfiguration, string topic) 
            : this(msSqlConfiguration, topic, new MsSqlConnectionProvider(msSqlConfiguration))
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
            s_logger.LogDebug("MsSqlMessagingConsumer: purging queue");
            _sqlMessageQueue.Purge();
        }
        
        public async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            s_logger.LogDebug("MsSqlMessagingConsumer: purging queue");
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
            return [message];
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
            
            var ct = new CancellationTokenSource();
            ct.CancelAfter(timeOut ?? TimeSpan.FromMilliseconds(300) );    
            var operationCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ct.Token).Token;
            
            var rc = await _sqlMessageQueue.TryReceiveAsync(_topic, operationCancellationToken);
            var message = !rc.IsDataValid ? new Message() : rc.Message;
            return [message];
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <remarks>
        ///  Not implemented for the MSSQL message consumer
        /// </remarks>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
         {
             s_logger.LogInformation(
                 "MsSqlMessagingConsumer: rejecting message with topic {Topic} and id {Id}, NOT IMPLEMENTED",
                 message.Header.Topic, message.Id);
         }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <remarks>
        ///  Not implemented for the MSSQL message consumer
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the reject</param>
        public Task RejectAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
         { 
             Reject(message); 
             return Task.CompletedTask;
         }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">Delay is not natively supported - don't block with Task.Delay</param>
        /// <returns>True when message is requeued</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;
            
            // delay is not natively supported - don't block with Task.Delay
            var topic = message.Header.Topic;

            s_logger.LogDebug("MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            _sqlMessageQueue.Send(message, topic); 
            return true;
        }
        
        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">Delay is not natively supported - don't block with Task.Delay</param>
        /// <returns>True when message is requeued</returns>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            delay ??= TimeSpan.Zero;
            
            // delay is not natively supported - don't block with Task.Delay
            var topic = message.Header.Topic;

            s_logger.LogDebug("MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            await _sqlMessageQueue.SendAsync(message, topic, null, cancellationToken: cancellationToken); 
            return true;
        }
        
        /// <summary>
        /// Dispose of the consumer
        /// </summary>
        /// <remarks>
        /// Nothing to do here
        /// </remarks>
        public void Dispose() {}
    }
}
