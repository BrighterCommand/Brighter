using System;
using System.Threading.Tasks;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Category("MQTT")]
    public class WhenQueueIsPurgedAsync : MqttTestClassBase<WhenQueueIsPurgedAsync>
    {
        private const string ClientId = "BrighterIntegrationTests-Purge";
        private const string TopicPrefix = "BrighterIntegrationTests/PurgeTests";

        public WhenQueueIsPurgedAsync()
        : base(ClientId, TopicPrefix)
        {
        }

        [Test]
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

            await Assert.That(receivedMessages).IsNotEmpty();
            await Assert.That(receivedMessages).HasSingleItem();
            await Assert.That(receivedMessages).Contains(_noopMessage);
        }
    }
}

