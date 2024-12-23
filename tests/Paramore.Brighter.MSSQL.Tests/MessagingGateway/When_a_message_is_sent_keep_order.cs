﻿using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway
{
    [Trait("Category", "MSSQL")]
    public class OrderTest
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _topicName = Guid.NewGuid().ToString();
        private readonly IAmAProducerRegistry _producerRegistry; 
        private readonly IAmAMessageConsumerSync _consumer;

        public OrderTest()
        {
            var testHelper = new MsSqlTestHelper();
            testHelper.SetupQueueDb();

            var routingKey = new RoutingKey(_topicName);
            
            var sub = new Subscription<MyCommand>(new SubscriptionName(_queueName),
                new ChannelName(_topicName), routingKey);
            _producerRegistry = new MsSqlProducerRegistryFactory(
                testHelper.QueueConfiguration, 
                new Publication[] {new() {Topic = routingKey}}
            ).Create();
            _consumer = new MsSqlMessageConsumerFactory(testHelper.QueueConfiguration).Create(sub);
        }

        [Fact]
        public void When_a_message_is_sent_keep_order()
        {
            IAmAMessageConsumerSync consumer = _consumer;
            try
            {
                //Send a sequence of messages to Kafka
                var msgId = SendMessage();
                var msgId2 = SendMessage();
                var msgId3 = SendMessage();
                var msgId4 = SendMessage();
                
                //Now read those messages in order

                var firstMessage = ConsumeMessages(consumer);
                var message = firstMessage.First();
                message.Empty.Should().BeFalse("A message should be returned");
                message.Id.Should().Be(msgId);

                var secondMessage = ConsumeMessages(consumer);
                message = secondMessage.First();
                message.Empty.Should().BeFalse("A message should be returned");
                message.Id.Should().Be(msgId2);

                var thirdMessages = ConsumeMessages(consumer);
                message = thirdMessages.First();
                message.Empty.Should().BeFalse("A message should be returned");
                message.Id.Should().Be(msgId3);

                var fourthMessage = ConsumeMessages(consumer);
                message = fourthMessage.First();
                message.Empty.Should().BeFalse("A message should be returned");
                message.Id.Should().Be(msgId4);

            }
            finally
            {
                consumer?.Dispose();
            }
        }

        private string SendMessage()
        {
            var messageId = Guid.NewGuid().ToString();

            var routingKey = new RoutingKey(_topicName);
            ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey)).Send(new Message(
                new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND),
                new MessageBody($"test content [{_queueName}]")));

            return messageId;
        }
        private IEnumerable<Message> ConsumeMessages(IAmAMessageConsumerSync consumer)
        {
            var messages = new Message[0];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                        break;
                }
                catch (ChannelFailureException)
                {
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    //_output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                }
            } while (maxTries <= 3);

            return messages;
        }

    }
}
