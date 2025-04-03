using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class MqttMessageProducerSendMessageTestsAsync : MqttTestClassBase<MqttMessageProducerSendMessageTestsAsync>
    {
        private const string ClientId = "BrighterIntegrationTests-Produce";
        private const string TopicPrefix = "BrighterIntegrationTests/ProducerTests";

        public MqttMessageProducerSendMessageTestsAsync(ITestOutputHelper testOutputHelper)
        : base(ClientId, TopicPrefix, testOutputHelper)
        {
        }

        [Fact]
        public async Task When_posting_multiples_message_via_the_messaging_gateway()
        {
            const int messageCount = 1000;
            List<Message> sentMessages = [];

            for (int i = 0; i < messageCount; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await MessageProducer.SendAsync(message);
                sentMessages.Add(message);
            }

            Message[] receivedMessages = await MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Equal(messageCount, receivedMessages.Length);
            Assert.Equal(receivedMessages, sentMessages);
        }
    }
}
