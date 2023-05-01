﻿using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using Acks = Confluent.Kafka.Acks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaMessageProducerMissingHeaderTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAMessageConsumer _consumer;
    private readonly IProducer<string,byte[]> _producer;

    public KafkaMessageProducerMissingHeaderTests(ITestOutputHelper output)
    {
        const string groupId = "Kafka Message Producer Missing Header Test";
        _output = output;
        
        
        var clientConfig = new ClientConfig
        {
            Acks = (Confluent.Kafka.Acks)((int)Acks.All),
            BootstrapServers = string.Join(",", new[] { "localhost:9092" }),
            ClientId = "Kafka Producer Send with Missing Header Tests", 
        };

        var producerConfig = new ProducerConfig(clientConfig)
        {
            BatchNumMessages = 10, 
            EnableIdempotence = true,
            MaxInFlight = 1,
            LingerMs = 5,
            MessageTimeoutMs = 5000,
            MessageSendMaxRetries = 3,
            Partitioner = Confluent.Kafka.Partitioner.ConsistentRandom,
            QueueBufferingMaxMessages = 10,
            QueueBufferingMaxKbytes =  1048576,
            RequestTimeoutMs = 500,
            RetryBackoffMs = 100,
        };
        
        _producer = new ProducerBuilder<string, byte[]>(producerConfig)
            .SetErrorHandler((_, error) =>
            {
                output.WriteLine($"Kafka producer failed with Code: {error.Code}, Reason: { error.Reason}, Fatal: {error.IsFatal}", error.Code, error.Reason, error.IsFatal);
            })
            .Build();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    makeChannels: OnMissingChannel.Create
                )
            );
    }

    [Fact]
    public void When_recieving_a_message_without_partition_key_header()
    {
        var command = new MyCommand { Value = "Test Content" };

        //vanilla i.e. no Kafka specific bytes at the beginning
        var body = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        var value = Encoding.UTF8.GetBytes(body);
        var kafkaMessage = new Confluent.Kafka.Message<string, byte[]>
        {
            Key = command.Id.ToString(), 
            Value = value
        };

       _producer.Produce(_topic, kafkaMessage, report => _output.WriteLine(report.ToString()) );

        var receivedMessage = GetMessage();

        //Where we lack a partition key header, assume non-Brighter header and set to message key
        receivedMessage.Header.PartitionKey.Should().Be(command.Id.ToString());
        receivedMessage.Body.Bytes.Should().Equal(value);
    }

    private Message GetMessage()
    {
        Message[] messages = new Message[0];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                Task.Delay(500).Wait(); //Let topic propagate in the broker
                messages = _consumer.Receive(1000);

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                {
                    _consumer.Acknowledge(messages[0]);
                    break;
                }
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        if (messages[0].Header.MessageType == MessageType.MT_NONE)
            throw new Exception($"Failed to read from topic:{_topic} after {maxTries} attempts");

        return messages[0];
    }

    public void Dispose()
    {
        _producer?.Dispose();
        _consumer?.Dispose();
    }
}
