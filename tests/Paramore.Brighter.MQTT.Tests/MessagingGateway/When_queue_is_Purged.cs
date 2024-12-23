﻿#region Licence
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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests.MessagingGateway
{
    [Trait("Category", "MQTT")]
    [Collection("MQTT")]
    public class When_queue_is_Purged : IDisposable, IAsyncDisposable
    {
        private const string MqttHost = "localhost";
        private const string ClientId = "BrighterIntegrationTests-Purge";
        private readonly IAmAMessageProducerAsync _messageProducer;
        private readonly IAmAMessageConsumerSync _messageConsumer;
        private readonly string _topicPrefix = "BrighterIntegrationTests/PurgeTests";
        private readonly Message _noopMessage = new();

        public When_queue_is_Purged()
        {

            var mqttProducerConfig = new MQTTMessagingGatewayProducerConfiguration
            {
                Hostname = MqttHost,
                TopicPrefix = _topicPrefix
            };

            MQTTMessagePublisher mqttMessagePublisher = new(
                mqttProducerConfig);

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
        public void When_purging_the_queue_on_the_messaging_gateway()
        {
            for (int i = 0; i < 5; i++)
            {
                Message _message = new(
                    new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND),
                    new MessageBody($"test message")
                );

                Task task = _messageProducer.SendAsync(_message);
                task.Wait();
            }

            Thread.Sleep(100);

            _messageConsumer.Purge();

            Message[] recievedMessages = _messageConsumer.Receive(TimeSpan.FromMilliseconds(100));

            recievedMessages.Should().NotBeEmpty()
            .And.HaveCount(1)
            .And.ContainInOrder(new[] { _noopMessage })
            .And.ContainItemsAssignableTo<Message>();
        }

        public void Dispose()
        {
            ((IAmAMessageProducerSync)_messageProducer).Dispose();
            _messageConsumer.Dispose();
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await _messageProducer.DisposeAsync();
            await ((IAmAMessageProducerAsync)_messageConsumer).DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
