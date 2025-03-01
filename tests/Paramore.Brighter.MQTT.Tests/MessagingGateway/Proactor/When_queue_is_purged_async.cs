using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class WhenQueueIsPurgedAsync : IAsyncDisposable, IDisposable
    {
        private const string MqttHost = "localhost";
        private const string ClientId = "BrighterIntegrationTests-Purge";
        private readonly IAmAMessageProducerAsync _messageProducer;
        private readonly IAmAMessageConsumerAsync _messageConsumer;
        private readonly string _topicPrefix = "BrighterIntegrationTests/PurgeTests";
        private readonly Message _noopMessage = new();

        public WhenQueueIsPurgedAsync()
        {
            var mqttProducerConfig = new MQTTMessagingGatewayProducerConfiguration
            {
                Hostname = MqttHost,
                TopicPrefix = _topicPrefix
            };

            MQTTMessagePublisher mqttMessagePublisher = new(mqttProducerConfig);
            _messageProducer = new MQTTMessageProducer(mqttMessagePublisher);

            MQTTMessagingGatewayConsumerConfiguration mqttConsumerConfig = new()
            {
                Hostname = MqttHost,
                TopicPrefix = _topicPrefix,
                ClientID = ClientId
            };

            _messageConsumer = new MQTTMessageConsumer(mqttConsumerConfig);
        }

        [Fact]
        public async Task WhenPurgingTheQueueOnTheMessagingGatewayAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await _messageProducer.SendAsync(message);
            }

            await Task.Delay(100);

            await _messageConsumer.PurgeAsync();

            Message[] receivedMessages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Single(receivedMessages);
            Assert.Contains(_noopMessage, receivedMessages);     
        }
        
        public void Dispose()
        {
            ((IAmAMessageProducerSync)_messageProducer).Dispose();
            ((IAmAMessageConsumerSync)_messageConsumer).Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _messageProducer.DisposeAsync();
            await _messageConsumer.DisposeAsync();
        }
    }
}
