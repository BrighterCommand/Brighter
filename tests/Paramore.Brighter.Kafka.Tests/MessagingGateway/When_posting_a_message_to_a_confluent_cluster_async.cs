using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using SaslMechanism = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism;
using SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Trait("Category", "Confluent")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaConfluentProducerSendTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaConfluentProducerSendTestsAsync(ITestOutputHelper output)
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

        const string groupId = "Kafka Message Producer Send Test";
        string bootStrapServer = Environment.GetEnvironmentVariable("CONFLUENT_BOOSTRAP_SERVER");
        string userName = Environment.GetEnvironmentVariable("CONFLUENT_SASL_USERNAME");
        string password = Environment.GetEnvironmentVariable("CONFLUENT_SASL_PASSWORD");

        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {bootStrapServer},
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanisms = SaslMechanism.Plain,
                SaslUsername = userName,
                SaslPassword = password,
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
            }).CreateAsync().Result;

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {bootStrapServer},
                    SecurityProtocol = SecurityProtocol.SaslSsl,
                    SaslMechanisms = SaslMechanism.Plain,
                    SaslUsername = userName,
                    SaslPassword = password,
                    SslCaLocation = SupplyCertificateLocation()
                })
            .CreateAsync(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName),
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    messagePumpType: MessagePumpType.Proactor,
                    makeChannels: OnMissingChannel.Create
                ));

    }

    [Fact]
    public async Task When_posting_a_message_to_a_confluent_cluster()
    {
        var routingKey = new RoutingKey(_topic);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey
            },
            new MessageBody($"test content [{_queueName}]"));
        await ((IAmAMessageProducerAsync)_producerRegistry.LookupAsyncBy(routingKey)).SendAsync(message);

        Message[] messages = new Message[0];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                await Task.Delay(500); //Let topic propagate in the broker
                messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000));
                await _consumer.AcknowledgeAsync(messages[0]);

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;

            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }

        } while (maxTries <= 3);

        messages.Length.Should().Be(1);
        messages[0].Header.MessageType.Should().Be(MessageType.MT_COMMAND);
        messages[0].Header.PartitionKey.Should().Be(_partitionKey);
        messages[0].Body.Value.Should().Be(message.Body.Value);
    }
    
    public void Dispose()
    {
        _producerRegistry?.Dispose();
        ((IAmAMessageConsumerSync)_consumer)?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _producerRegistry.Dispose();
        await _consumer.DisposeAsync();
    }
}
