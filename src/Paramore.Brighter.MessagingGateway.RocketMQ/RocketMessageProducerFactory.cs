using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Factory class for creating RocketMQ message producers in Brighter.
/// Implements RocketMQ's producer group pattern and transactional message support.
/// </summary>
/// <param name="connection">The gateway connection configuration.</param>
/// <param name="publications">The publications to create producers for.</param>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create the logger.</param>
public partial class RocketMessageProducerFactory(RocketMessagingGatewayConnection connection, IEnumerable<RocketMqPublication> publications, ILoggerFactory? loggerFactory = null) : IAmAMessageProducerFactory
{
    private readonly ILogger _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<RocketMessageProducerFactory>();
    
    /// <inheritdoc />
    public Dictionary<ProducerKey, IAmAMessageProducer> Create() 
        => BrighterAsyncContext.Run(() => CreateAsync());

    /// <inheritdoc />
    public async Task<Dictionary<ProducerKey, IAmAMessageProducer>> CreateAsync()
    {
        var rocketProducer = await CreateProducerAsync();
        var producers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            if (RoutingKey.IsNullOrEmpty(publication.Topic))
            {
                throw new ConfigurationException("A Rocket publication must have a topic");
            }

            if (publication.MakeChannels == OnMissingChannel.Create)
            {
                Log.CreateTopicIsNotSupported(_logger, publication.Topic!.Value);
            }
            
            producers[new ProducerKey(publication.Topic, publication.Type)] = new RocketMqMessageProducer(connection,
                rocketProducer,
                publication,
                publication.Instrumentation ?? connection.Instrumentation);
        }
        return producers;
    }

    private async Task<Producer> CreateProducerAsync()
    {
        var builder = new Producer.Builder();
        builder.SetClientConfig(connection.ClientConfig)
            .SetMaxAttempts(connection.MaxAttempts)
            .SetTopics(publications
                .Where(x =>  !RoutingKey.IsNullOrEmpty(x.Topic))
                .Select(x => x.Topic!.Value)
                .ToArray());

        if (connection.Checker != null)
        {
            builder.SetTransactionChecker(connection.Checker);
        }
        
        return await builder.Build(); 
    }
    
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "RocketMQ doesn't support create topic via code ({Topic})")]
        public static partial void CreateTopicIsNotSupported(ILogger logger, string topic);
    }
}
