using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageConsumer : IAmAMessageConsumer
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

        public MsSqlMessageConsumer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            string topic) :this(msSqlConfiguration, topic, new MsSqlConnectionProvider(msSqlConfiguration))
        {
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeOut">How long to wait on a recieve. Default is 300ms</param>
        /// <returns>Message.</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            timeOut ??= TimeSpan.FromMilliseconds(300);
            
            var rc = _sqlMessageQueue.TryReceive(_topic, timeOut.Value);
            var message = !rc.IsDataValid ? new Message() : rc.Message;
            return new Message[]{message};
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            // Not required because of atomic 'read-and-delete'
        }

         /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Reject(Message message)
         {
             s_logger.LogInformation(
                 "MsSqlMessagingConsumer: rejecting message with topic {Topic} and id {Id}, NOT IMPLEMENTED",
                 message.Header.Topic, message.Id.ToString());
         }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            s_logger.LogDebug("MsSqlMessagingConsumer: purging queue");
            _sqlMessageQueue.Purge();
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Delay to delivery of the message. 0 for immediate requeue. Default to 0</param>
        /// <returns>True when message is requeued</returns>
        public bool Requeue(Message message, TimeSpan? delay = null)
        {
            delay ??= TimeSpan.Zero;

            //TODO: This blocks, use a time evern instead to requeue after an interval
            if (delay.Value > TimeSpan.Zero)
            {
                Task.Delay(delay.Value).Wait();
            }

            var topic = message.Header.Topic;

            s_logger.LogDebug("MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            _sqlMessageQueue.Send(message, topic); 
            return true;
        }
        
        public void Dispose()
        {
        }
    }
}
