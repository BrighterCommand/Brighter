using System;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageConsumer : IAmAMessageConsumer
    {
        private readonly string _topic;
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageConsumer>);
        private readonly MsSqlMessageQueue<Message> _sqlQ;

        public MsSqlMessageConsumer(MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration, string topic)
        {
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlMessagingGatewayConfiguration);
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
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message, bool requeue)
        {
            Logger.Value.Info(
                $"MsSqlMessagingConsumer: rejecting message with topic {message.Header.Topic} and id {message.Id.ToString()}, NOT IMPLEMENTED");
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        public void Purge()
        {
            Logger.Value.Debug("MsSqlMessagingConsumer: purging queue");
            _sqlQ.Purge();
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message)
        {
            var topic = message.Header.Topic;

            Logger.Value.Debug(
                $"MsSqlMessagingConsumer: requeuing message with topic {topic} and id {message.Id.ToString()}");

            _sqlQ.Send(message, topic);
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds).Wait();
            Requeue(message);
        }
        
        public void Dispose()
        {
        }
    }
}
