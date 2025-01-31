using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The <see cref="SqsMessageProducer"/> factory
/// </summary>
public class SqsMessageProducerFactory : IAmAMessageProducerFactory
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly IEnumerable<SqsPublication> _publications;

    /// <summary>
    /// Initialize new instance of <see cref="SqsMessageProducerFactory"/>.
    /// </summary>
    /// <param name="connection">The <see cref="AWSMessagingGatewayConnection"/>.</param>
    /// <param name="publications">The collection of <see cref="SqsPublication"/>.</param>
    public SqsMessageProducerFactory(AWSMessagingGatewayConnection connection,
        IEnumerable<SqsPublication> publications)
    {
        _connection = connection;
        _publications = publications;
    }

    /// <inheritdoc />
    /// <remarks>
    ///  Sync over async used here, alright in the context of producer creation
    /// </remarks>
    public Dictionary<RoutingKey, IAmAMessageProducer> Create()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var sqs in _publications)
        {
            if (sqs.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var producer = new SqsMessageProducer(_connection, sqs);
            if (producer.ConfirmQueueExists())
            {
                producers[sqs.Topic] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SQS queue: {sqs.Topic}");
            }
        }

        return producers;
    }

    /// <inheritdoc />
    public async Task<Dictionary<RoutingKey, IAmAMessageProducer>> CreateAsync()
    {
        var producers = new Dictionary<RoutingKey, IAmAMessageProducer>();
        foreach (var sqs in _publications)
        {
            if (sqs.Topic is null)
            {
                throw new ConfigurationException("Missing topic on Publication");
            }

            var producer = new SqsMessageProducer(_connection, sqs);
            if (await producer.ConfirmQueueExistsAsync())
            {
                producers[sqs.Topic] = producer;
            }
            else
            {
                throw new ConfigurationException($"Missing SQS queue: {sqs.Topic}");
            }
        }

        return producers;
    }
}
