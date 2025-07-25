using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Paramore.Brighter.MessagingGateway.Pulsar;

namespace Paramore.Brighter.Pulsar.Tests.Utils;

public static class GatewayFactory
{
    public static PulsarMessagingGatewayConnection CreateConnection()
    {
        return new PulsarMessagingGatewayConnection
        {
            ServiceUrl = new Uri("pulsar://localhost:6650"),
        };
    }

    public static IConsumer<ReadOnlySequence<byte>> CreateConsumer(PulsarMessagingGatewayConnection connection, Publication publication)
    {
        return connection.Create().NewConsumer(Schema.ByteSequence)
            .Topic(publication.Topic!.Value)
            .SubscriptionName(Guid.NewGuid().ToString())
            .Create();
    }
    
    public static IProducer<ReadOnlySequence<byte>> CreateProducer(PulsarMessagingGatewayConnection connection, Publication publication)
    {
        return connection.Create().NewProducer(Schema.ByteSequence)
            .Topic(publication.Topic!.Value)
            .Create();
    }
}
