using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// Class responsible for sending a message to a SQS
/// </summary>
public partial class SqsMessageSender
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsMessageSender>();
    private static readonly TimeSpan s_maxDelay = TimeSpan.FromSeconds(900);
    
    private readonly string _queueUrl;
    private readonly SqsType _queueType;
    private readonly AmazonSQSClient _client;

    /// <summary>
    /// Initialize the <see cref="SqsMessageSender"/>
    /// </summary>
    /// <param name="queueUrl">The queue ARN</param>
    /// <param name="queueType">The queue type</param>
    /// <param name="client">The SQS Client</param>
    public SqsMessageSender(string queueUrl, SqsType queueType, AmazonSQSClient client)
    {
        _queueUrl = queueUrl;
        _queueType = queueType;
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
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl, 
            MessageBody = message.Body.Value
        };

        delay ??= TimeSpan.Zero;
        if (delay > TimeSpan.Zero)
        {
            // SQS has a hard limit of 15min for Delay in Seconds
            if (delay.Value > s_maxDelay)
            {
                delay = s_maxDelay;
                Log.DelaySetToMaximum(s_logger, delay);
            }

            request.DelaySeconds = (int)delay.Value.TotalSeconds;
        }

        if (_queueType == SqsType.Fifo)
        {
            request.MessageGroupId = message.Header.PartitionKey;
            if (message.Header.Bag.TryGetValue(HeaderNames.DeduplicationId, out var deduplicationId))
            {
                request.MessageDeduplicationId = (string)deduplicationId;
            }
        }
        
        // Combine cloud event headers into a single JSON object
        string cloudEventHeadersJson = CreateCloudEventHeadersJson(message);

        // we can set up to 10 attributes;  we use a single JSON object as the cloud event headers; we can set nine others directly 
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        { 
            [HeaderNames.Id] = new (){ StringValue = message.Header.MessageId, DataType = "String" },
            [HeaderNames.CloudEventHeaders] = new() { StringValue = cloudEventHeadersJson, DataType = "String" },
            [HeaderNames.Topic] = new() { StringValue = _queueUrl ,DataType = "String" },
            [HeaderNames.MessageType] = new() { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.ContentType] = new() { StringValue = message.Header.ContentType, DataType = "String" },
            [HeaderNames.Timestamp] = new() { StringValue = Convert.ToString(message.Header.TimeStamp.ToRcf3339()), DataType = "String" }
        };
        
        if (!string.IsNullOrEmpty(message.Header.ReplyTo))
            messageAttributes.Add(HeaderNames.ReplyTo, new MessageAttributeValue { StringValue = message.Header.ReplyTo, DataType = "String" });

        if (!string.IsNullOrEmpty(message.Header.Subject))
            messageAttributes.Add(HeaderNames.Subject, new MessageAttributeValue { StringValue = message.Header.Subject, DataType = "String" });

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
            messageAttributes.Add(HeaderNames.CorrelationId,  new MessageAttributeValue { StringValue = message.Header.CorrelationId, DataType = "String" });
        
        //we have to add some attributes into our bag, to prevent overloading the message attributes
        message.Header.Bag[HeaderNames.HandledCount] = message.Header.HandledCount.ToString(CultureInfo.InvariantCulture);
        
        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new() { StringValue = bagJson, DataType = "String" };
        request.MessageAttributes = messageAttributes;

        var response = await _client.SendMessageAsync(request, cancellationToken);
        if (response.HttpStatusCode is HttpStatusCode.OK or HttpStatusCode.Created
            or HttpStatusCode.Accepted)
        {
            return response.MessageId;
        }

        return null;
    }

    private static string CreateCloudEventHeadersJson(Message message)
    {
        var cloudEventHeaders = new Dictionary<string, string>
        {
            [HeaderNames.Id] = Convert.ToString(message.Header.MessageId),
            [HeaderNames.DataContentType] = message.Header.ContentType ?? "plain/text",
            [HeaderNames.DataSchema] = message.Header.DataSchema?.ToString() ?? string.Empty,
            [HeaderNames.SpecVersion] = message.Header.SpecVersion,
            [HeaderNames.Type] = message.Header.Type,
            [HeaderNames.Source] = message.Header.Source.ToString(),
            [HeaderNames.Time] = message.Header.TimeStamp.ToRcf3339()
        };

        if (!string.IsNullOrEmpty(message.Header.Subject))
            cloudEventHeaders[HeaderNames.Subject] = message.Header.Subject!;

        if (message.Header.DataSchema != null)
            cloudEventHeaders[HeaderNames.DataSchema] = message.Header.DataSchema.ToString();

        if (message.Header.DataRef != null)
            cloudEventHeaders[HeaderNames.DataRef] = message.Header.DataRef;

        var cloudEventHeadersJson = JsonSerializer.Serialize(cloudEventHeaders, JsonSerialisationOptions.Options);
        return cloudEventHeadersJson;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Set delay from {CurrentDelay} to 15min (SQS support up to 15min)")]
        public static partial void DelaySetToMaximum(ILogger logger, TimeSpan? currentDelay);
    }
}

