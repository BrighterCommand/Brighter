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
        private readonly MsSqlMessageQueue<Message> _sqlQ;

        public MsSqlMessageConsumer(
            MsSqlConfiguration msSqlConfiguration, 
            string topic, IMsSqlConnectionProvider connectionProvider)
        {
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectionProvider);
        }

        public MsSqlMessageConsumer(
            MsSqlConfiguration msSqlConfiguration,
            string topic) :this(msSqlConfiguration, topic, new MsSqlSqlAuthConnectionProvider(msSqlConfiguration))
        {
        }

        /// <summary>
        /// Receives the specified queue name.
        /// An abstraction over a third-party messaging library. Used to read messages from the broker and to acknowledge the processing of those messages or requeue them.
        /// Used by a <see cref="Channel"/> to provide access to a third-party message queue.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            var rc = _sqlQ.TryReceive(_topic, timeoutInMilliseconds);
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
        public bool Reject(Message message)
         {
             s_logger.LogInformation(
                 "MsSqlMessagingConsumer: rejecting message with topic {Topic} and id {Id}, NOT IMPLEMENTED",
                 message.Header.Topic, message.Id.ToString());
             return false;
         }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            s_logger.LogDebug("MsSqlMessagingConsumer: purging queue");
            _sqlQ.Purge();
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        /// <returns>True when message is requeued</returns>
        public bool Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds).Wait();
            var topic = message.Header.Topic;

            s_logger.LogDebug("MsSqlMessagingConsumer: re-queuing message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            _sqlQ.Send(message, topic); 
            return true;
        }
        
        public void Dispose()
        {
        }
    }
}
