namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud Pub/Sub registry factory
/// </summary>
/// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
/// <param name="publications">The collection of <see cref="PubSubPublication"/>.</param>
public class PubSubRegistryFactory(
    GcpMessagingGatewayConnection connection,
    IEnumerable<PubSubPublication> publications) : IAmAProducerRegistryFactory
{
    /// <inheritdoc />
    public IAmAProducerRegistry Create()
    {
        var factory = new PubSubProducerFactory(connection, publications);
        return new ProducerRegistry(factory.Create());
    }

    /// <inheritdoc />
    public async Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
    {
        var factory = new PubSubProducerFactory(connection, publications);
        return new ProducerRegistry(await factory.CreateAsync());
    }
}
