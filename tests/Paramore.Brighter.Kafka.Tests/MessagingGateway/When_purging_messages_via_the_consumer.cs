#region Licence
/* The MIT License (MIT)
Copyright © 2014 Wayne Hunsley <whunsley@gmail.com>

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
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Collection("Kafka")]
    [Trait("Category", "Kafka")]
    public class KafkaMessageConsumerPurgeTests : KafkaIntegrationTestBase
    {
        private const string Topic = "test";

        private string QueueName { get; set; }

        public KafkaMessageConsumerPurgeTests()
        {
            QueueName = Guid.NewGuid().ToString();
        }

        [Theory, MemberData(nameof(ServerParameters))]
        public void When_purging_messages_via_the_messaging_gateway(string bootStrapServer)
        {
            using (var consumer = CreateMessageConsumer<MyCommand>("TestConsumer", bootStrapServer, QueueName, Topic))
            using (var producer = CreateMessageProducer("TestProducer", bootStrapServer))
            {
                consumer.Receive(30000);
                var messages = Enumerable.Range(0, 10).Select((i => CreateMessage(Topic, $"test content [{QueueName}] count [{i}]")));
                foreach (var msg in messages)
                {
                    producer.Send(msg);
                }
                
                consumer.Purge();
                var sentMessage = consumer.Receive(30000).Single();
                var messageBody = sentMessage.Body.Value;
                messageBody.Should().Be(string.Empty);
            }
        }
    }
}
