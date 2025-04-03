﻿using System;
using System.Threading.Tasks;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Proactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class WhenQueueIsPurgedAsync : MqttTestClassBase<WhenQueueIsPurgedAsync>
    {
        private const string ClientId = "BrighterIntegrationTests-Purge";
        private const string TopicPrefix = "BrighterIntegrationTests/PurgeTests";

        public WhenQueueIsPurgedAsync(ITestOutputHelper testOutputHelper)
        : base(ClientId, TopicPrefix, testOutputHelper)
        {
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

                await MessageProducer.SendAsync(message);
            }

            await Task.Delay(100);

            await MessageConsumer.PurgeAsync();

            Message[] receivedMessages = await MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Single(receivedMessages);
            Assert.Contains(_noopMessage, receivedMessages);
        }
    }
}
