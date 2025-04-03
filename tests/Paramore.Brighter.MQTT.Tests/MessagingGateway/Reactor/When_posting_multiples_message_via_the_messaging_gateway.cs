#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.MQTT.Tests.MessagingGateway.Helpers.Base;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway.Reactor
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class MqttMessageProducerSendMessageTests : MqttTestClassBase<MqttMessageProducerSendMessageTests>
    {
        private const string ClientId = "BrighterIntegrationTests-Produce";
        private const string TopicPrefix = "BrighterIntegrationTests/ProducerTests";

        public MqttMessageProducerSendMessageTests(ITestOutputHelper testOutputHelper)
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
                Message _message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                await MessageProducer.SendAsync(_message);

                sentMessages.Add(_message);
            }

            Message[] receivedMessages = await MessageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100));

            Assert.NotEmpty(receivedMessages);
            Assert.Equal(messageCount, receivedMessages.Length);
            Assert.Equal(sentMessages, receivedMessages);
        }
    }
}
