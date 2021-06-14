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
        private readonly IAmAMessageProducer _producer;
        private readonly IAmAMessageConsumer _consumer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();
        private string _deferQueueName;
        private IAmAMessageConsumer _deferConsumer;

        public KafkaRequeueGoesToNewTopicIfDefined()
        {
            const string groupId = "Kafka Message Producer Send Test";
            _deferQueueName = $"{_topic}_Defer_{5}";
            
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {"localhost:9092"}
                },
                new KafkaPublication()
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }).Create();

            var kafkaConfiguration = new KafkaMessagingGatewayConfiguration {Name = "Kafka Consumer Test", BootStrapServers = new[] {"localhost:9092"}}; 
            _consumer = new KafkaMessageConsumerFactory(kafkaConfiguration)
                .Create(new KafkaSubscription<MyCommand>(
                        channelName: new ChannelName(_queueName), 
                        routingKey: new RoutingKey(_topic),
                        groupId: groupId,
                        numOfPartitions: 1,
                        replicationFactor: 1,
                        requeueDelayInMs:5,
                        deferRoutingKey: new RoutingKey(_deferQueueName),
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
        }
    }
}
