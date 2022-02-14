using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    public class MQTTMessageProducer : IAmAMessageProducer, IAmAMessageProducerAsync, IAmAMessageProducerSync
    {
        public int MaxOutStandingMessages { get; set; } = -1;
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;
        private MQTTMessagePublisher _mqttMessagePublisher;

        public MQTTMessageProducer(MQTTMessagingGatewayConfiguration configuration)
        {
            _mqttMessagePublisher = new MQTTMessagePublisher(configuration);
        }

        public MQTTMessageProducer(MQTTMessagePublisher mqttMessagePublisher)
        {
            _mqttMessagePublisher = mqttMessagePublisher;
        }

        public void Dispose()
        {
            _mqttMessagePublisher = null;
        }

        public void Send(Message message)
        {
            System.Diagnostics.Debugger.Break();
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _mqttMessagePublisher.PublishMessage(message);
        }

        public async Task SendAsync(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _mqttMessagePublisher.PublishMessageAsync(message);
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Task.Delay(delayMilliseconds);
            Send(message);
        }
    }
}
