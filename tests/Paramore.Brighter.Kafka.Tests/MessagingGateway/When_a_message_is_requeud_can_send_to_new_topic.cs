using System;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    public class KafkaRequeueGoesToNewTopicIfDefined 
    {
        private readonly string _queueName = Guid.NewGuid().ToString();
        private readonly string _requeueName = Guid.NewGuid().ToString();
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly string _requeueTopic = Guid.NewGuid().ToString();
        private readonly IAmAMessageConsumer _consumer;
        private IAmAMessageConsumer _deferConsumer;

        public KafkaRequeueGoesToNewTopicIfDefined()
        {
            const string groupId = "Kafka Message Producer Send Test";
            string deferQueueName = $"{_topic}_Defer_{5}";
            
            var kafkaConfiguration = new KafkaMessagingGatewayConfiguration {Name = "Kafka Consumer Test", BootStrapServers = new[] {"localhost:9092"}}; 
            _consumer = new KafkaMessageConsumerFactory(kafkaConfiguration)
                .Create(new KafkaSubscription<MyCommand>(
                        channelName: new ChannelName(_queueName), 
                        routingKey: new RoutingKey(_topic),
                        groupId: groupId,
                        numOfPartitions: 1,
                        replicationFactor: 1,
                        requeueDelayInMs:5,
                        deferRoutingKey: new RoutingKey(deferQueueName),
                        makeChannels: OnMissingChannel.Create
                    )
                );
            
            
            _deferConsumer = new KafkaMessageConsumerFactory(kafkaConfiguration)
                .Create(new KafkaSubscription<MyCommand>(
                        channelName: new ChannelName(_requeueName), 
                        routingKey: new RoutingKey(_requeueTopic),
                        groupId: groupId,
                        numOfPartitions: 1,
                        replicationFactor: 1,
                        requeueDelayInMs:5,
                        makeChannels: OnMissingChannel.Create
                    )
                );

        }

        [Fact]
        public void When_a_message_is_requeued_send_to_new_topic()
        {
            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND) {PartitionKey = Guid.NewGuid().ToString()},
                new MessageBody($"test content [{_queueName}]")
                );
            
            //reject from the first consumer
            _consumer.Reject(message);
            
            //should now be on the second consumer
            var rejectedMessage = _consumer.Receive(5000);
        }
    }
}
