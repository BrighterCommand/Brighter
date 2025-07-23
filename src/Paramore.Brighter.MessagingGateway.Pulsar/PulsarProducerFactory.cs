using System.Collections.Generic;
using System.Threading.Tasks;
using DotPulsar;
using DotPulsar.Extensions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarProducerFactory(PulsarMessagingGatewayConnection connection, IEnumerable<PulsarPublication> publications) : IAmAMessageProducerFactory
{
    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
    {
        var client = connection.Create();
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (publication.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var builder = client.NewProducer(publication.Schema)
                .CompressionType(publication.CompressionType)
                .InitialSequenceId(publication.InitialSequenceId)
                .ProducerAccessMode(publication.AccessMode)
                .Topic(publication.Topic);

            var producerName = publication.Name ?? connection.ProducerName;
            if (!string.IsNullOrWhiteSpace(producerName))
            {
                builder = builder.ProducerName(producerName!);
            }
            
            publication.Configure?.Invoke(builder);

            var producer = builder.Create();
            
            producers[publication.Topic] = new PulsarProducer(producer,
                publication, 
                publication.TimeProvider, 
                publication.Instrumentation ?? connection.Instrumentation);
        }

        return producers;
    }

    /// <inheritdoc />
    public Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync() 
        => Task.FromResult(Create());
}
