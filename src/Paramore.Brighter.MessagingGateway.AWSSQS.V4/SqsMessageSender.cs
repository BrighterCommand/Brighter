using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Class responsible for sending a message to a SQS
/// </summary>
public partial class SqsMessageSender
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsMessageSender>();
    private static readonly TimeSpan s_maxDelay = TimeSpan.FromSeconds(900);
    
    private readonly string _queueUrl;
    private readonly AmazonSQSClient _client;

    /// <summary>
    /// Initialize the <see cref="SqsMessageSender"/>
    /// </summary>
    /// <param name="queueUrl">The queue ARN</param>
    /// <param name="client">The SQS Client</param>
    public SqsMessageSender(string queueUrl, AmazonSQSClient client)
    {
        _queueUrl = queueUrl;
        _client = client;
    }
    
    /// <summary>
    /// Sending message via SQS
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">The delay in ms. 0 is no delay. Defaults to 0</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that cancels the Publish operation</param>
    /// <returns>The message id.</returns>
    public async Task<string?> SendAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken)
    {
        var request = CreateSendMessageRequest(message, delay);

        var response = await _client.SendMessageAsync(request, cancellationToken);
        if (response.HttpStatusCode is HttpStatusCode.OK or HttpStatusCode.Created
            or HttpStatusCode.Accepted)
        {
            return response.MessageId;
        }

        return null;
    }

    private SendMessageRequest CreateSendMessageRequest(Message message, TimeSpan? delay)
    {
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = message.Body.Value
        };

        SetMessageDelay(request, delay);
        SetFifoQueueProperties(request, message);
        SetMessageAttributes(request, message);

        return request;
    }

    private static void SetMessageDelay(SendMessageRequest request, TimeSpan? delay)
    {
        delay ??= TimeSpan.Zero;
        if (delay > TimeSpan.Zero)
        {
            if (delay.Value > s_maxDelay)
            {
                delay = s_maxDelay;
                Log.DelaySetToMaximum(s_logger, delay);
            }

            request.DelaySeconds = (int)delay.Value.TotalSeconds;
        }
    }

    private static void SetFifoQueueProperties(SendMessageRequest request, Message message)
    {
        if (PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            return;
        }
        
        request.MessageGroupId = message.Header.PartitionKey;
        if (message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
        {
            request.MessageDeduplicationId = (string)deduplicationId;
        }
    }

    private void SetMessageAttributes(SendMessageRequest request, Message message)
    {
        string cloudEventHeadersJson = CreateCloudEventHeadersJson(message);

        var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            [HeaderNames.Id] = new() { StringValue = message.Header.MessageId, DataType = "String" },
            [HeaderNames.CloudEventHeaders] = new() { StringValue = cloudEventHeadersJson, DataType = "String" },
            [HeaderNames.Topic] = new() { StringValue = _queueUrl, DataType = "String" },
            [HeaderNames.MessageType] = new() { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.ContentType] = new() { StringValue = contentType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new() { StringValue = Convert.ToString(message.Header.TimeStamp.ToRfc3339()), DataType = "String" }
        };

        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
            messageAttributes.Add(HeaderNames.ReplyTo, new MessageAttributeValue { StringValue = message.Header.ReplyTo, DataType = "String" });

        if (!string.IsNullOrEmpty(message.Header.Subject))
            messageAttributes.Add(HeaderNames.Subject, new MessageAttributeValue { StringValue = message.Header.Subject, DataType = "String" });

        if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
            messageAttributes.Add(HeaderNames.CorrelationId, new MessageAttributeValue { StringValue = message.Header.CorrelationId, DataType = "String" });

        message.Header.Bag[HeaderNames.HandledCount] = message.Header.HandledCount.ToString(CultureInfo.InvariantCulture);

        var bagJson = System.Text.Json.JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new() { StringValue = bagJson, DataType = "String" };
        request.MessageAttributes = messageAttributes;
    }

    private static string CreateCloudEventHeadersJson(Message message)
    {
        var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
        var cloudEventHeaders = new Dictionary<string, string>
        {
            [HeaderNames.Id] = Convert.ToString(message.Header.MessageId),
            [HeaderNames.DataContentType] = contentType.ToString(),
            [HeaderNames.DataSchema] = message.Header.DataSchema?.ToString() ?? string.Empty,
            [HeaderNames.SpecVersion] = message.Header.SpecVersion,
            [HeaderNames.Type] = message.Header.Type,
            [HeaderNames.Source] = message.Header.Source.ToString(),
            [HeaderNames.Time] = message.Header.TimeStamp.ToRfc3339()
        };

        if (!string.IsNullOrEmpty(message.Header.Subject))
            cloudEventHeaders[HeaderNames.Subject] = message.Header.Subject!;

        if (message.Header.DataSchema != null)
            cloudEventHeaders[HeaderNames.DataSchema] = message.Header.DataSchema.ToString();

        if (message.Header.DataRef != null)
            cloudEventHeaders[HeaderNames.DataRef] = message.Header.DataRef;

        var cloudEventHeadersJson = System.Text.Json.JsonSerializer.Serialize(cloudEventHeaders, JsonSerialisationOptions.Options);
        return cloudEventHeadersJson;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Set delay from {CurrentDelay} to 15min (SQS support up to 15min)")]
        public static partial void DelaySetToMaximum(ILogger logger, TimeSpan? currentDelay);
    }
}
