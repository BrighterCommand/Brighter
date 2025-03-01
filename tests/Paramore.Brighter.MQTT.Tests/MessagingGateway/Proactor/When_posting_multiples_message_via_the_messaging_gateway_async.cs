using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class MqttMessageProducerSendMessageTestsAsync : IAsyncDisposable, IDisposable
    {
        private const string MqttHost = "localhost";
        private const string ClientId = "BrighterIntegrationTests-Produce";
        private readonly IAmAMessageProducerAsync _messageProducer;
        private readonly IAmAMessageConsumerAsync _messageConsumer;
        private readonly string _topicPrefix = "BrighterIntegrationTests/ProducerTests";

        public MqttMessageProducerSendMessageTestsAsync()
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
        public async Task When_posting_multiples_message_via_the_messaging_gateway()
        {
            const int messageCount = 1000;
            List<Message> sentMessages = new();

            for (int i = 0; i < messageCount; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await _messageProducer.SendAsync(message);
                sentMessages.Add(message);
            }

            Message[] receivedMessages = await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Equal(messageCount, receivedMessages.Length);
            Assert.Equal(receivedMessages, sentMessages);    
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
