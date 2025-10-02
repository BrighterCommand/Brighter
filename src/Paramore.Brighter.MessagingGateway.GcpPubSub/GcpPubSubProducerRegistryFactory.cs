namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// A factory class responsible for creating a <see cref="IAmAProducerRegistry"/> containing
/// <see cref="GcpMessageProducer"/> instances for Google Cloud Pub/Sub.
/// It uses the <see cref="GcpPubSubMessageProducerFactory"/> to manage the creation and initialization
/// of the underlying Pub/Sub <see cref="Google.Cloud.PubSub.V1.PublisherClient"/> instances.
/// </summary>
/// <param name="connection">The connection details for the Google Cloud Pub/Sub gateway.</param>
/// <param name="publications">A collection of Google Cloud Pub/Sub specific publication configurations.</param>
public class GcpPubSubProducerRegistryFactory(
    GcpMessagingGatewayConnection connection,
    IEnumerable<GcpPublication> publications) : IAmAProducerRegistryFactory
{
    /// <summary>
    /// Creates the synchronous producer registry. This method wraps the asynchronous creation.
    /// </summary>
    /// <returns>An instance of <see cref="IAmAProducerRegistry"/> populated with <see cref="GcpMessageProducer"/> instances.</returns>
    public IAmAProducerRegistry Create()
    {
        // Create the message producer factory
        var factory = new GcpPubSubMessageProducerFactory(connection, publications);

        // Synchronously create the dictionary of producers and wrap it in a ProducerRegistry
        return new ProducerRegistry(factory.Create());
    }

    /// <summary>
    /// Asynchronously creates the producer registry.
    /// This is the preferred method for creating the registry as it allows for asynchronous initialization
    /// of the Google Cloud Pub/Sub clients and the validation/creation of topics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that returns an instance of <see cref="IAmAProducerRegistry"/>.</returns>
    public async Task<IAmAProducerRegistry> CreateAsync(CancellationToken cancellationToken = default)
    {
        // Create the message producer factory
        var factory = new GcpPubSubMessageProducerFactory(connection, publications);

        // Asynchronously create the dictionary of producers
        var producers = await factory.CreateAsync();

        // Wrap the dictionary of producers in a ProducerRegistry
        return new ProducerRegistry(producers);
    }
}
