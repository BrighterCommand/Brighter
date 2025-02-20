using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ message producer
/// </summary>
public class RocketMessageProducerFactory(RocketMessagingGatewayConnection connection, IEnumerable<RocketPublication> publications) : IAmAMessageProducerFactory
{
    /// <inheritdoc />
    public Dictionary<RoutingKey, IAmAMessageProducer> Create() 
        => BrighterAsyncContext.Run(async () => await CreateAsync());

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var rocketProducer = await CreateProducerAsync();
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (publication.Topic is null || RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                throw new ConfigurationException("A Rocket publication must have a topic");
            }
            
            producers[publication.Topic] = new RocketMessageProducer(connection, rocketProducer, publication);
        }
        return producers;
    }

    private async Task<Producer> CreateProducerAsync()
    {
        var builder = new Producer.Builder();

        builder.SetClientConfig(connection.ClientConfig)
            .SetMaxAttempts(connection.MaxAttempts)
            .SetTopics(publications
                .Where(x => x.Topic is not null && !RoutingKey.IsNullOrEmpty(x.Topic))
                .Select(x => x.Topic!.Value)
                .ToArray());

        if (connection.Checker != null)
        {
            builder.SetTransactionChecker(connection.Checker);
        }
        
        return await builder.Build(); 
    }
}
