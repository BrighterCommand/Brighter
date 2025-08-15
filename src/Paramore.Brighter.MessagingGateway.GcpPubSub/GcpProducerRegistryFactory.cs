namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud Pub/Sub registry factory
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
/// <param name="publications">The collection of <see cref="GcpMessageProducer"/>.</param>
public class GcpProducerRegistryFactory(
    GcpMessagingGatewayConnection connection,
    IEnumerable<GcpPublication> publications) : IAmAProducerRegistryFactory
{
    /// <inheritdoc />
    public IAmAProducerRegistry Create()
    {
        var factory = new GcpMessageProducerFactory(connection, publications);
        return new ProducerRegistry(factory.Create());
    }

    /// <inheritdoc />
    public async Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
    {
        var factory = new GcpMessageProducerFactory(connection, publications);
        return new ProducerRegistry(await factory.CreateAsync());
    }
}
