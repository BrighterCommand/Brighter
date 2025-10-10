using System;
using System.Threading.Tasks;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class WhenQueueIsPurgedAsync() : MqttTestClassBase<WhenQueueIsPurgedAsync>(ClientId, TopicPrefix)
    {
        private const string ClientId = "BrighterIntegrationTests-Purge";
        private const string TopicPrefix = "BrighterIntegrationTests/PurgeTests";

        [Fact]
        public async Task WhenPurgingTheQueueOnTheMessagingGatewayAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await MessageProducerAsync.SendAsync(message);
            }

            await Task.Delay(100);

            await MessageConsumerAsync.PurgeAsync();

            Message[] receivedMessages = await MessageConsumerAsync.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Single(receivedMessages);
            Assert.Contains(_noopMessage, receivedMessages);
        }
    }
}
