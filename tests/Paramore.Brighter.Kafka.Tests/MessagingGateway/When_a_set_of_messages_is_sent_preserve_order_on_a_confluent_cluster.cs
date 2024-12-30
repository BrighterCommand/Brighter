using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Trait("Category", "Confluent")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerConfluentPreservesOrder : IDisposable
{
    private const string _groupId = "Kafka Message Producer Assume Topic Test";
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly string _bootStrapServer;
    private readonly string _userName;
    private readonly string _password;


    public KafkaMessageConsumerConfluentPreservesOrder(ITestOutputHelper output)
    {
        // -- Confluent supply these values, see their .NET examples for your account
        // You need to set those values as environment variables, which we then read, in order
        // to run these tests

        _bootStrapServer = Environment.GetEnvironmentVariable("CONFLUENT_BOOSTRAP_SERVER"); 
        _userName = Environment.GetEnvironmentVariable("CONFLUENT_SASL_USERNAME");
        _password = Environment.GetEnvironmentVariable("CONFLUENT_SASL_PASSWORD");
            
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {_bootStrapServer},
                SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol.SaslSsl,
                SaslMechanisms = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism.Plain,
                SaslUsername = _userName,
                SaslPassword = _password,
                SslCaLocation = SupplyCertificateLocation()
                    
            },
            new[] {new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 3,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 10000,
                    MakeChannels = OnMissingChannel.Create //This will not make the topic
                }
            }).Create();
    }

    [Fact]
    public void When_a_message_is_sent_keep_order()
    {
        IAmAMessageConsumerSync consumer = null;
        try
        {
            //Send a sequence of messages to Kafka
            var msgId = SendMessage();
            var msgId2 = SendMessage();
            var msgId3 = SendMessage();
            var msgId4 = SendMessage();
                  
            consumer = CreateConsumer();
                
            //Now read those messages in order
                
            var firstMessage = ConsumeMessages(consumer);
            var message = firstMessage.First();
            message.Id.Should().Be(msgId);
            consumer.Acknowledge(message);

            var secondMessage = ConsumeMessages(consumer);
            message = secondMessage.First();
            message.Id.Should().Be(msgId2);
            consumer.Acknowledge(message);               
                
            var thirdMessages = ConsumeMessages(consumer);
            message = thirdMessages .First();
            message.Id.Should().Be(msgId3);
            consumer.Acknowledge(message);               
                
            var fourthMessage = ConsumeMessages(consumer);
            message = fourthMessage .First();
            message.Id.Should().Be(msgId4);
            consumer.Acknowledge(message);               
 
        }
        finally
        {
            consumer?.Dispose();
        }
    }

    private string SendMessage()
    {
        var messageId = Guid.NewGuid().ToString();

        var routingKey = new RoutingKey(_topic);
            
        ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey)).Send(
            new Message(
                new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]")
            )
        );

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
                Task.Delay(500).Wait(); //Let topic propagate in the broker
                messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        return messages;
    }

    private IAmAMessageConsumerSync CreateConsumer()
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {_bootStrapServer},
                    SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol.SaslSsl,
                    SaslMechanisms = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism.Plain,
                    SaslUsername = _userName,
                    SaslPassword = _password,
                    SslCaLocation = SupplyCertificateLocation()
                    
                })
            .Create(new KafkaSubscription<MyCommand>(
                channelName: new ChannelName(_queueName), 
                routingKey: new RoutingKey(_topic),
                groupId: _groupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize:1,
                numOfPartitions: 1,
                replicationFactor: 3,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
        
    private string SupplyCertificateLocation()
    {
        //For different platforms, we have to figure out how to get the connection right
        //see: https://docs.confluent.io/platform/current/tutorials/examples/clients/docs/csharp.html
                
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/usr/local/etc/openssl@1.1/cert.pem" : null;
    }
}
