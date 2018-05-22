using System;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync
    {
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageProducer>);
        private readonly MsSqlMessageQueue<Message> _sqlQ;

        public MsSqlMessageProducer(MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration)
        {
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlMessagingGatewayConfiguration);
        }

        public void Send(Message message)
        {
            var topic = message.Header.Topic;

            Logger.Value.Debug($"MsSqlMessageProducer: send message with topic {topic} and id {message.Id.ToString()}");

            _sqlQ.Send(message, topic);
        }
        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
   

        public async Task SendAsync(Message message)
        {
            var topic = message.Header.Topic;

            Logger.Value.Debug(
                $"MsSqlMessageProducer: send async message with topic {topic} and id {message.Id.ToString()}");

            await _sqlQ.SendAsync(message, topic);
        }

        public void Dispose()
        {
        }
    }
}
