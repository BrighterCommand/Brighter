#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public class SnsMessagePublisher
{
    private readonly string _topicArn;
    private readonly AmazonSimpleNotificationServiceClient _client;

    public SnsMessagePublisher(string topicArn, AmazonSimpleNotificationServiceClient client)
    {
        _topicArn = topicArn;
        _client = client;
    }

    public async Task<string?> PublishAsync(Message message)
    {
        var publishRequest = CreatePublishRequest(message);
        
        var response = await _client.PublishAsync(publishRequest);
        if (response.HttpStatusCode is System.Net.HttpStatusCode.OK or System.Net.HttpStatusCode.Created
            or System.Net.HttpStatusCode.Accepted)
        {
            return response.MessageId;
        }

        return null;
    }

    private PublishRequest CreatePublishRequest(Message message)
    {
        var publishRequest = new PublishRequest(_topicArn, message.Body.Value, message.Header.Subject);
        
        ConfigureFifoSettings(message, publishRequest);

        var cloudEventHeadersJson = CreateCloudEventHeadersJson(message);
        publishRequest.MessageAttributes = BuildMessageAttributes(message, cloudEventHeadersJson);

        return publishRequest;
    }

    private static void ConfigureFifoSettings(Message message, PublishRequest request)
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

    private Dictionary<string, MessageAttributeValue> BuildMessageAttributes(Message message, string cloudEventHeadersJson)
    {
        var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
        var messageAttributes = new Dictionary<string, MessageAttributeValue>
        {
            [HeaderNames.Id] = new (){ StringValue = message.Header.MessageId, DataType = "String" },
            [HeaderNames.CloudEventHeaders] = new() { StringValue = cloudEventHeadersJson, DataType = "String" },
            [HeaderNames.Topic] = new() { StringValue = _topicArn, DataType = "String" },
            [HeaderNames.MessageType] = new() { StringValue = message.Header.MessageType.ToString(), DataType = "String" },
            [HeaderNames.ContentType] = new() { StringValue = contentType.ToString(), DataType = "String" },
            [HeaderNames.Timestamp] = new() { StringValue = Convert.ToString(message.Header.TimeStamp.ToRfc3339()), DataType = "String" },
        };

        if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
            messageAttributes[HeaderNames.CorrelationId] = new MessageAttributeValue 
                { StringValue = Convert.ToString(message.Header.CorrelationId), DataType = "String" };
        
        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
            messageAttributes.Add(HeaderNames.ReplyTo, new MessageAttributeValue { StringValue = Convert.ToString(message.Header.ReplyTo), DataType = "String" });
        
        message.Header.Bag[HeaderNames.HandledCount] = message.Header.HandledCount.ToString(CultureInfo.InvariantCulture);

        var bagJson = JsonSerializer.Serialize(message.Header.Bag, JsonSerialisationOptions.Options);
        messageAttributes[HeaderNames.Bag] = new MessageAttributeValue { StringValue = Convert.ToString(bagJson), DataType = "String" };

        return messageAttributes;
    }

    private static string CreateCloudEventHeadersJson(Message message)
    {
        var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
        var cloudEventHeaders = new Dictionary<string, string>
        {
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

        var cloudEventHeadersJson = JsonSerializer.Serialize(cloudEventHeaders, JsonSerialisationOptions.Options);
        return cloudEventHeadersJson;
    }
}
