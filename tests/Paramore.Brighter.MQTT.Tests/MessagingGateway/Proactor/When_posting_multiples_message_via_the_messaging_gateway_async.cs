using System;
using System.Collections.Generic;
using System.Threading;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="Proactor.MqttMessageProducerSendMessageTestsAsync "/> class.
        /// </summary>
        /// <param name="testOutputHelper">The output helper for capturing test output during execution.</param>
        /// <remarks>
        /// This constructor sets up the MQTT messaging gateway test environment by configuring the client ID, topic prefix, 
        /// and test output helper. It leverages the base class <see cref="MqttTestClassBase{T}"/> to initialize the necessary 
        /// MQTT configurations and logging mechanisms.
        /// </remarks>
        public MqttMessageProducerSendMessageTestsAsync(ITestOutputHelper testOutputHelper)
        : base(ClientId, TopicPrefix, testOutputHelper)
        {
        }

        [Fact]
        public async Task When_posting_multiples_message_via_the_messaging_gateway_async()
        {
            const int messageCount = 1000;
            List<Message> sentMessages = [];

            for (int i = 0; i < messageCount; i++)
            {
                Message message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await MessageProducerAsync.SendAsync(message);
                sentMessages.Add(message);
            }

            Message[] receivedMessages = await MessageConsumerAsync.ReceiveAsync(TimeSpan.FromMilliseconds(200));

            // Spin until we receive all messages or timeout after 2 seconds.
            Assert.True(SpinWait.SpinUntil(() => receivedMessages.Length == messageCount, 5000), $"Received {receivedMessages.Length} of {messageCount} messages.");

            Assert.NotEmpty(receivedMessages);
            Assert.Equal(messageCount, receivedMessages.Length);
            Assert.Equal(receivedMessages, sentMessages);
        }
    }
}
