using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer factory.
/// </summary>
public class GcpMessageProducerFactory : GcpPubSubMessageGateway, IAmAMessageProducerFactory
{
    private readonly InstrumentationOptions? _instrumentation;
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly IEnumerable<GcpPublication> _publications;

    /// <summary>
    /// The Google Cloud PubSub producer factory.
    /// </summary>
    /// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="GcpPublication"/>.</param>
    /// <param name="instrumentation">The <see cref="InstrumentationOptions"/>.</param>
    public GcpMessageProducerFactory(GcpMessagingGatewayConnection connection,
        IEnumerable<GcpPublication> publications,
        InstrumentationOptions? instrumentation = null) : base(connection)
    {
        _connection = connection;
        _publications = publications;
        _instrumentation = instrumentation;
    }

    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
        => BrighterAsyncContext.Run(async () => await CreateAsync());

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();

        foreach (var publication in _publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            publication.TopicAttributes ??= new TopicAttributes();
            if (string.IsNullOrEmpty(publication.TopicAttributes.Name))
            {
                publication.TopicAttributes.Name = publication.Topic;
            }

            await EnsureTopicExistAsync(publication.TopicAttributes, publication.MakeChannels);
            producers[publication.Topic] = new GcpMessageProducer(_connection, publication, _instrumentation ?? InstrumentationOptions.None);
        }

        return producers;
    }
}
