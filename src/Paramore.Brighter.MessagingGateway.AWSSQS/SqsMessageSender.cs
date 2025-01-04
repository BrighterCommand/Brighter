using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// Class responsible for sending a message to a SQS
/// </summary>
public class SqsMessageSender
{
    private readonly string _queueUrl;
    private readonly SnsSqsType _queueType;
    private readonly AmazonSQSClient _client;

    /// <summary>
    /// Initialize the <see cref="SqsMessageSender"/>
    /// </summary>
    /// <param name="queueUrl">The queue ARN</param>
    /// <param name="queueType">The queue type</param>
    /// <param name="client">The SQS Client</param>
    public SqsMessageSender(string queueUrl, SnsSqsType queueType, AmazonSQSClient client)
    {
        _queueUrl = queueUrl;
        _queueType = queueType;
        _client = client;
    }

    public async Task<string?> SendAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken)
    {
        var request = new SendMessageRequest { QueueUrl = _queueUrl, MessageBody = message.Body.Value, };

        if (delay != null)
        {
            request.DelaySeconds = (int)delay.Value.TotalSeconds;
        }

        if (_queueType == SnsSqsType.Fifo)
        {
            request.MessageGroupId = message.Header.PartitionKey;
            if (message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
            {
                request.MessageDeduplicationId = (string)deduplicationId;
            }
        }

        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            [HeaderNames.Id] =
                new() { StringValue = Convert.ToString(message.Header.MessageId), DataType = "String" },
            [HeaderNames.Topic] = new() { StringValue = _queueUrl, DataType = "String" },
            [HeaderNames.ContentType] = new() { StringValue = message.Header.ContentType, DataType = "String" },
            [HeaderNames.CorrelationId] =
                new() { StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String" },
            [HeaderNames.HandledCount] =
                new() { StringValue = Convert.ToString(message.Header.HandledCount), DataType = "String" },
            [HeaderNames.MessageType] =
                new() { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new()
            {
                StringValue = Convert.ToString(message.Header.TimeStamp), DataType = "String"
            }
        };

        if (!string.IsNullOrEmpty(message.Header.ReplyTo))
        {
            messageAttributes.Add(HeaderNames.ReplyTo,
                new MessageAttributeValue { StringValue = message.Header.ReplyTo, DataType = "String" });
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            messageAttributes.Add(HeaderNames.Subject,
                new MessageAttributeValue { StringValue = message.Header.Subject, DataType = "String" });
        }

        // we can set up to 10 attributes; we have set 6 above, so use a single JSON object as the bag
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new() { StringValue = Convert.ToString(bagJson), DataType = "String" };
        request.MessageAttributes = messageAttributes;

        var response = await _client.SendMessageAsync(request, cancellationToken);
        if (response.HttpStatusCode is HttpStatusCode.OK or HttpStatusCode.Created
            or HttpStatusCode.Accepted)
        {
            return response.MessageId;
        }

        return null;
    }
}
