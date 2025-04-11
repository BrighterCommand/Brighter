using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Google Cloud PubSub producer factory.
/// </summary>
public class PubSubProducerFactory : PubSubMessageGateway, IAmAMessageProducerFactory
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly IEnumerable<PubSubPublication> _publications;

    /// <summary>
    /// The Google Cloud PubSub producer factory.
    /// </summary>
    /// <param name="connection">The <see cref="GcpMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="PubSubPublication"/>.</param>
    public PubSubProducerFactory(GcpMessagingGatewayConnection connection,
        IEnumerable<PubSubPublication> publications) : base(connection)
    {
        _connection = connection;
        _publications = publications;
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

            await EnsureTopicExistAsync(publication.TopicAttributes, publication.MakeChannels);

            var builder = new PublisherServiceApiClientBuilder();
            
            _connection.PublishConfiguration?.Invoke(builder);

            var publisher = await builder.BuildAsync(); 
            
            var topicName = TopicName.FromProjectTopic(publication.TopicAttributes.ProjectId ?? Connection.ProjectId, publication.TopicAttributes.Name);
            producers[publication.Topic] = new PubSubProducer(publisher, topicName, publication);
        }

        return producers;
    }
}
