using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;
        
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducer>();
        private readonly MsSqlMessageQueue<Message> _sqlQ;
        private Publication _publication; // -- placeholder for future use

        public MsSqlMessageProducer(
            MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration,
            Publication publication = null)
        {
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlMessagingGatewayConfiguration);
            _publication = publication ?? new Publication() {MakeChannels = OnMissingChannel.Create};
            MaxOutStandingMessages = _publication.MaxOutStandingMessages;
            MaxOutStandingCheckIntervalMilliSeconds = _publication.MaxOutStandingCheckIntervalMilliSeconds;
        }

        public void Send(Message message)
        {
            var topic = message.Header.Topic;

            s_logger.LogDebug($"MsSqlMessageProducer: send message with topic {topic} and id {message.Id.ToString()}");

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

            s_logger.LogDebug(
                $"MsSqlMessageProducer: send async message with topic {topic} and id {message.Id.ToString()}");

            await _sqlQ.SendAsync(message, topic);
        }

        public void Dispose()
        {
        }
    }
}
