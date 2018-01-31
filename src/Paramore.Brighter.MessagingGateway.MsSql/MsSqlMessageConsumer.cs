using System;
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

        public Message Receive(int timeoutInMilliseconds)
        {
            var rc = _sqlQ.TryReceive(_topic, timeoutInMilliseconds);
            return !rc.IsDataValid ? new Message() : rc.Message;
        }

        public void Acknowledge(Message message)
        {
            // Not required because of atomic 'read-and-delete'
        }

        public void Reject(Message message, bool requeue)
        {
            Logger.Value.Info(
                $"MsSqlMessagingConsumer: rejecting message with topic {message.Header.Topic} and id {message.Id.ToString()}, NOT IMPLEMENTED");
        }

        public void Purge()
        {
            Logger.Value.Debug("MsSqlMessagingConsumer: purging queue");
            _sqlQ.Purge();
        }

        public void Requeue(Message message)
        {
            var topic = message.Header.Topic;

            Logger.Value.Debug(
                $"MsSqlMessagingConsumer: requeuing message with topic {topic} and id {message.Id.ToString()}");

            _sqlQ.Send(message, topic);
        }

        public void Dispose()
        {
        }
    }
}