using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The <see cref="ValidateQueueByName"/> class is responsible for validating an AWS SQS queue by its name.
/// </summary>
public class ValidateQueueByName : IValidateQueue, IDisposable
{
    private readonly AmazonSQSClient _client;
    private readonly SqsType _type;

    /// <summary>
    /// Initialize new instance of <see cref="ValidateTopicByName"/>. 
    /// </summary>
    /// <param name="client">The <see cref="AmazonSQSClient"/> client.</param>
    /// <param name="type">The SQS type.</param>
    public ValidateQueueByName(AmazonSQSClient client, SqsType type)
    {
        _client = client;
        _type = type;
    }
    
    /// <summary>
    /// Validates that a queue exists by name.
    /// We use the GetQueueUrlAsync method to check if the queue exists.
    /// </summary>
    /// <param name="queue">The name of the queue</param>
    /// <param name="cancellationToken">Cancels the ongoing operation</param>
    /// <returns></returns>
    public async Task<(bool, string?)> ValidateAsync(string queue, CancellationToken cancellationToken = default)
    {
        try
        {
            queue = queue.ToValidSQSQueueName(_type == SqsType.Fifo);
            var queueUrlResponse = await _client.GetQueueUrlAsync(queue, cancellationToken);
            return (queueUrlResponse.HttpStatusCode == HttpStatusCode.OK, queueUrlResponse.QueueUrl);
        }
        catch (QueueDoesNotExistException)
        {
            return (false, queue);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _client.Dispose();
}
