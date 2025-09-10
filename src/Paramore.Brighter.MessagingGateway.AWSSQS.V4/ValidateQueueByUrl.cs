using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The <see cref="ValidateQueueByUrl"/> class is responsible for validating an AWS SQS queue by its url.
/// </summary>
public class ValidateQueueByUrl : IValidateQueue, IDisposable
{
    private readonly AmazonSQSClient _client;

    /// <summary>
    /// Initialize new instance of <see cref="ValidateQueueByUrl"/>.
    /// </summary>
    /// <param name="client">The <see cref="AmazonSQSClient"/> client.</param>
    public ValidateQueueByUrl(AmazonSQSClient client)
    {
        _client = client;
    }

    /// <inheritdoc cref="IValidateQueue.ValidateAsync"/>
    public async Task<(bool, string?)> ValidateAsync(string queue, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _client.GetQueueAttributesAsync(queue, [QueueAttributeName.QueueArn], cancellationToken);
            return (true, queue);
        }
        catch (QueueDoesNotExistException)
        {
            return (false, queue);
        }
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _client.Dispose();
}
