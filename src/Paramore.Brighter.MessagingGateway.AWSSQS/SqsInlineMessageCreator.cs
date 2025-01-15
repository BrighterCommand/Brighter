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
using System.Text.Json;
using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

internal class SqsInlineMessageCreator : SqsMessageCreatorBase, ISqsMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsInlineMessageCreator>();

    private Dictionary<string, JsonElement> _messageAttributes = new();

    public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
    {
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<string?>.Empty();

        Message message;
        try
        {
            var jsonDocument = JsonDocument.Parse(sqsMessage.Body);
            _messageAttributes = ReadMessageAttributes(jsonDocument);

            topic = ReadTopic();
            messageId = ReadMessageId();
            var contentType = ReadContentType();
            var correlationId = ReadCorrelationId();
            var handledCount = ReadHandledCount();
            var messageType = ReadMessageType();
            var timeStamp = ReadTimestamp();
            var replyTo = ReadReplyTo();
            var subject = ReadMessageSubject(jsonDocument);
            var receiptHandle = ReadReceiptHandle(sqsMessage);
            var partitionKey = ReadPartitionKey(sqsMessage);
            var deduplicationId = ReadDeduplicationId(sqsMessage);

            //TODO:CLOUD_EVENTS parse from headers

            var messageHeader = new MessageHeader(
                messageId: messageId.Result ?? string.Empty,
                topic: topic.Result ?? RoutingKey.Empty,
                messageType: messageType.Result,
                source: null,
                type: "",
                timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                correlationId: correlationId.Success ? correlationId.Result : string.Empty,
                replyTo: replyTo.Result is not null ? new RoutingKey(replyTo.Result) : RoutingKey.Empty,
                contentType: contentType.Result ?? "plain/text",
                handledCount: handledCount.Result,
                dataSchema: null,
                subject: subject.Result,
                delayed: TimeSpan.Zero,
                partitionKey: partitionKey.Result ?? string.Empty
            );

            message = new Message(messageHeader, ReadMessageBody(jsonDocument));

            //deserialize the bag 
            var bag = ReadMessageBag();
            foreach (var key in bag.Keys)
            {
                message.Header.Bag.Add(key, bag[key]);
            }

            if (deduplicationId.Success)
            {
                message.Header.Bag[HeaderNames.DeduplicationId] = deduplicationId.Result;
            }

            if (receiptHandle.Success)
            {
                message.Header.Bag.Add("ReceiptHandle", sqsMessage.ReceiptHandle);
            }
        }
        catch (Exception e)
        {
            s_logger.LogWarning(e, "Failed to create message from Aws Sqs message");
            message = FailureMessage(topic, messageId);
        }

        return message;
    }

    private static Dictionary<string, JsonElement> ReadMessageAttributes(JsonDocument jsonDocument)
    {
        var messageAttributes = new Dictionary<string, JsonElement>();

        try
        {
            if (jsonDocument.RootElement.TryGetProperty("MessageAttributes", out var attributes))
            {
                messageAttributes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    attributes.GetRawText(),
                    JsonSerialisationOptions.Options);
            }
        }
        catch (Exception ex)
        {
            s_logger.LogWarning($"Failed while deserializing Sqs Message body, ex: {ex}");
        }

        return messageAttributes ?? new Dictionary<string, JsonElement>();
    }

    private HeaderResult<string?> ReadContentType()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.ContentType, out var contentType))
        {
            return new HeaderResult<string?>(contentType.GetValueInString(), true);
        }

        return new HeaderResult<string?>(string.Empty, true);
    }

    private Dictionary<string, object> ReadMessageBag()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Bag, out var headerBag))
        {
            try
            {
                var json = headerBag.GetValueInString();
                if (string.IsNullOrEmpty(json))
                {
                    return new Dictionary<string, object>();
                }

                var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(json!,
                    JsonSerialisationOptions.Options);

                return bag ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                //suppress any errors in deserialization
            }
        }

        return new Dictionary<string, object>();
    }

    private HeaderResult<string?> ReadReplyTo()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.ReplyTo, out var replyTo))
        {
            return new HeaderResult<string?>(replyTo.GetValueInString(), true);
        }

        return new HeaderResult<string?>(string.Empty, true);
    }

    private HeaderResult<DateTime> ReadTimestamp()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Timestamp, out var timeStamp))
        {
            if (DateTime.TryParse(timeStamp.GetValueInString(), out var value))
            {
                return new HeaderResult<DateTime>(value, true);
            }
        }

        return new HeaderResult<DateTime>(DateTime.UtcNow, true);
    }

    private HeaderResult<MessageType> ReadMessageType()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.MessageType, out var messageType))
        {
            if (Enum.TryParse(messageType.GetValueInString(), out MessageType value))
            {
                return new HeaderResult<MessageType>(value, true);
            }
        }

        return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
    }

    private HeaderResult<int> ReadHandledCount()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.HandledCount, out var handledCount))
        {
            if (int.TryParse(handledCount.GetValueInString(), out var value))
            {
                return new HeaderResult<int>(value, true);
            }
        }

        return new HeaderResult<int>(0, true);
    }

    private HeaderResult<string?> ReadCorrelationId()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.CorrelationId, out var correlationId))
        {
            return new HeaderResult<string?>(correlationId.GetValueInString(), true);
        }

        return new HeaderResult<string?>(string.Empty, true);
    }

    private HeaderResult<string?> ReadMessageId()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Id, out var messageId))
        {
            return new HeaderResult<string?>(messageId.GetValueInString(), true);
        }

        return new HeaderResult<string?>(string.Empty, true);
    }

    private HeaderResult<RoutingKey> ReadTopic()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Topic, out var topicArn))
        {
            var topic = topicArn.GetValueInString() ?? string.Empty;

            if (Arn.TryParse(topic, out var arn))
            {
                return new HeaderResult<RoutingKey>(new RoutingKey(arn.Resource), true);
            }

            var indexOf = topic.LastIndexOf('/');
            if (indexOf != -1)
            {
                return new HeaderResult<RoutingKey>(new RoutingKey(topic.Substring(indexOf + 1)), true);
            }
            
            return new HeaderResult<RoutingKey>(new RoutingKey(topic), true);
        }

        return new HeaderResult<RoutingKey>(RoutingKey.Empty, true);
    }

    private static HeaderResult<string?> ReadMessageSubject(JsonDocument jsonDocument)
    {
        try
        {
            if (jsonDocument.RootElement.TryGetProperty("Subject", out var value))
            {
                return new HeaderResult<string?>(value.GetString(), true);
            }
        }
        catch (Exception ex)
        {
            s_logger.LogWarning($"Failed to parse Sqs Message Body to valid Json Document, ex: {ex}");
        }

        return new HeaderResult<string?>(null, true);
    }

    private static MessageBody ReadMessageBody(JsonDocument jsonDocument)
    {
        try
        {
            if (jsonDocument.RootElement.TryGetProperty("Message", out var value))
            {
                return new MessageBody(value.GetString());
            }
        }
        catch (Exception ex)
        {
            s_logger.LogWarning($"Failed to parse Sqs Message Body to valid Json Document, ex: {ex}");
        }

        return new MessageBody(string.Empty);
    }

    private static HeaderResult<string> ReadPartitionKey(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var value))
        {
            //we have an arn, and we want the topic
            var messageGroupId = value;
            return new HeaderResult<string>(messageGroupId, true);
        }

        return new HeaderResult<string>(string.Empty, false);
    }

    private static HeaderResult<string> ReadDeduplicationId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageDeduplicationId, out var value))
        {
            //we have an arn, and we want the topic
            var messageGroupId = value;
            return new HeaderResult<string>(messageGroupId, true);
        }

        return new HeaderResult<string>(string.Empty, false);
    }
}
