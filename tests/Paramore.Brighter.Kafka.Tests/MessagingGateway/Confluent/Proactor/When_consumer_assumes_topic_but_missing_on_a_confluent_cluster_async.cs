using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Confluent.Proactor;

[Trait("Category", "Kafka")]
[Trait("Category", "Confluent")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaConfluentProducerAssumeTestsAsync : IDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString(); 
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaConfluentProducerAssumeTestsAsync()
    {
        string SupplyCertificateLocation()
        {
            //For different platforms, we have to figure out how to get the connection right
            //see: https://docs.confluent.io/platform/current/tutorials/examples/clients/docs/csharp.html
                
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/usr/local/etc/openssl@1.1/cert.pem" : null;
        }
            
        // -- Confluent supply these values, see their .NET examples for your account
        // You need to set those values as environment variables, which we then read, in order
        // to run these tests

        string bootStrapServer = Environment.GetEnvironmentVariable("CONFLUENT_BOOSTRAP_SERVER"); 
        string userName = Environment.GetEnvironmentVariable("CONFLUENT_SASL_USERNAME");
        string password = Environment.GetEnvironmentVariable("CONFLUENT_SASL_PASSWORD");
            
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {bootStrapServer},
                SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol.SaslSsl,
                SaslMechanisms = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism.Plain,
                SaslUsername = userName,
                SaslPassword = password,
                SslCaLocation = SupplyCertificateLocation()
                    
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 3,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 10000,
                    MakeChannels = OnMissingChannel.Assume //This will not make the topic
                }
            ]).Create(); 
  
    }

    [Fact]
    public async Task When_a_consumer_declares_topics_on_a_confluent_cluster()
    {
        var routingKey = new RoutingKey(_topic);
            
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey
            },
            new MessageBody($"test content [{_queueName}]")
        );

        bool messagePublished = false;
            
            
        var producer = _producerRegistry.LookupBy(routingKey);
        var producerConfirm = producer as ISupportPublishConfirmation;
        producerConfirm.OnMessagePublished += delegate(bool success, string id)
        {
            if (success) messagePublished = true;
        };
            
        await ((IAmAMessageProducerAsync)producer).SendAsync(message);

        //Give this a chance to succeed - will fail
        await Task.Delay(5000);

        messagePublished.Should().BeFalse();
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}
