using System.Collections.Generic;
using System.Threading.Tasks;
using DotPulsar.Extensions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Factory for creating Pulsar message producers based on publication configurations
/// </summary>
/// <param name="connection">Shared Pulsar connection configuration</param>
/// <param name="publications">Collection of publication definitions</param>
public class PulsarProducerFactory(PulsarMessagingGatewayConnection connection, IEnumerable<PulsarPublication> publications) : IAmAMessageProducerFactory
{
    /// <summary>
    /// Creates message producers.
    /// </summary>
    /// <returns>A dictionary of middleware clients by topic/routing key, for sending messages to the middleware</returns>
    public Dictionary<ProducerKey, IAmAMessageProducer> Create()
    {
        var client = connection.Create();
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
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
            
            producers[new ProducerKey(publication.Topic, publication.Type)] = new PulsarMessageProducer(producer,
                publication, 
                publication.TimeProvider, 
                publication.Instrumentation ?? connection.Instrumentation);
        }

        return producers;
    }

    /// <summary>
    /// Creates message producers.
    /// </summary>
    /// <returns>A dictionary of middleware clients by topic/routing key, for sending messages to the middleware</returns>
    public Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync() 
        => Task.FromResult(Create());
}
