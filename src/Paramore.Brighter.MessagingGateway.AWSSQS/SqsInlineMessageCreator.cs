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
using System.Net.Mime;
using System.Text.Json;
using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

internal sealed partial class SqsInlineMessageCreator : SqsMessageCreatorBase, ISqsMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsInlineMessageCreator>();

    private Dictionary<string, JsonElement> _messageAttributes = new();

    public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
    {
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<Id?>.Empty();

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
            var type = ReadType();
            var source = ReadSource();
            var dataSchema = ReadDataSchema();
            var specVersion = ReadSpecVersion();

            var messageHeader = new MessageHeader(
                messageId: messageId.Result ?? Id.Empty,
                topic: topic.Result ?? RoutingKey.Empty,
                messageType: messageType.Result,
                source: source.Result,
                type: type.Result,
                timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                correlationId: correlationId.Success ? correlationId.Result : string.Empty,
                replyTo: replyTo.Result is not null ? new RoutingKey(replyTo.Result) : RoutingKey.Empty,
                contentType: contentType.Result ?? new ContentType(MediaTypeNames.Text.Plain),
                handledCount: handledCount.Result,
                dataSchema: dataSchema.Result,
                subject: subject.Result,
                delayed: TimeSpan.Zero,
                partitionKey: partitionKey.Result ?? string.Empty
            )
            {
                SpecVersion = specVersion.Result!
            };

            message = new Message(messageHeader, ReadMessageBody(jsonDocument));

            //deserialize the bag 
            var bag = ReadMessageBag();
            foreach (var keyValue in bag)
            {
                message.Header.Bag.Add(keyValue.Key, keyValue.Value);
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
            Log.FailedToCreateMessageFromAwsSqsMessage(s_logger, e);
            message = Message.FailureMessage(topic.Result, messageId.Result);
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
            Log.FailedWhileDeserializingSqsMessageBody(s_logger, ex);
        }

        return messageAttributes ?? new Dictionary<string, JsonElement>();
    }

    private HeaderResult<ContentType?> ReadContentType()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.DataContentType, out var contentType))
        {
            var result = contentType.GetValueInString();
            return new HeaderResult<ContentType?>(result is not null ? new ContentType(result) : new ContentType(MediaTypeNames.Text.Plain), true);
        }
        
        if (_messageAttributes.TryGetValue(HeaderNames.ContentType, out contentType))
        {
            var result = contentType.GetValueInString() ?? MediaTypeNames.Text.Plain;
            return new HeaderResult<ContentType?>(new ContentType(result), true);
        }

        return new HeaderResult<ContentType?>(null, true);
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

    private HeaderResult<RoutingKey?> ReadReplyTo()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.ReplyTo, out var replyTo))
        {
            var result = replyTo.GetValueInString();
            return new HeaderResult<RoutingKey?>(result is not null ? new RoutingKey(result) : RoutingKey.Empty, true);
        }

        return new HeaderResult<RoutingKey?>(RoutingKey.Empty, true);
    }

    private HeaderResult<string?> ReadSpecVersion()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.SpecVersion, out var specVersion))
        {
            return new HeaderResult<string?>(specVersion.GetValueInString(), true);
        }

        return new HeaderResult<string?>(MessageHeader.DefaultSpecVersion, true);
    }

    private HeaderResult<CloudEventsType?> ReadType()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Type, out var specVersion))
        {
            return new HeaderResult<CloudEventsType?>(new CloudEventsType(specVersion.GetValueInString() ?? string.Empty), true);
        }

        return new HeaderResult<CloudEventsType?>(CloudEventsType.Empty, true);
    }
    
    private HeaderResult<Uri> ReadSource()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Source, out var source))
        {
            if (Uri.TryCreate(source.GetValueInString(), UriKind.RelativeOrAbsolute, out var uri))
            {
                return new HeaderResult<Uri>(uri, true);
            }
        }

        return new HeaderResult<Uri>(new Uri(MessageHeader.DefaultSource), true);
    }
    
     private HeaderResult<Uri?> ReadDataSchema()
     {
         if (_messageAttributes.TryGetValue(HeaderNames.DataSchema, out var source))
         {
             if (Uri.TryCreate(source.GetValueInString(), UriKind.RelativeOrAbsolute, out var uri))
             {
                 return new HeaderResult<Uri?>(uri, true);
             }
         }
    
         return new HeaderResult<Uri?>(null, true);
     }

    private HeaderResult<DateTime> ReadTimestamp()
    {
         if (_messageAttributes.TryGetValue(HeaderNames.Time, out var timeStamp))
         {
             if (DateTime.TryParse(timeStamp.GetValueInString(), out var value))
             {
                 return new HeaderResult<DateTime>(value, true);
             }
         }
         if (_messageAttributes.TryGetValue(HeaderNames.Timestamp, out timeStamp))
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

    private HeaderResult<Id?> ReadCorrelationId()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.CorrelationId, out var correlationId))
        {
            var result = correlationId.GetValueInString();
            return new HeaderResult<Id?>(result is not null ? new Id(result) : Id.Empty, true);
        }

        return new HeaderResult<Id?>(Id.Empty, true);
    }

    private HeaderResult<Id?> ReadMessageId()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Id, out var messageId))
        {
            var value = messageId.GetValueInString();
            return new HeaderResult<Id?>(string.IsNullOrEmpty(value) ? Id.Random() : Id.Create(value), true);
        }

        return new HeaderResult<Id?>(Id.Random(), true);
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

    private HeaderResult<string?> ReadMessageSubject(JsonDocument jsonDocument)
    {
          if (_messageAttributes.TryGetValue(HeaderNames.Subject, out var messageId))
          {
              return new HeaderResult<string?>(messageId.GetValueInString(), true);
          }
          
          try
          {
              if (jsonDocument.RootElement.TryGetProperty("Subject", out var value))
              {
                  return new HeaderResult<string?>(value.GetString(), true);
              }
          }
          catch (Exception ex)
          {
              Log.FailedToParseSqsMessageBodyToValidJsonDocument(s_logger, ex);
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
            Log.FailedToParseSqsMessageBodyToValidJsonDocument(s_logger, ex);
        }

        return new MessageBody(string.Empty);
    }

    private static HeaderResult<PartitionKey> ReadPartitionKey(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var value))
        {
            //we have an arn, and we want the topic
            return new HeaderResult<PartitionKey>(value, true);
        }

        return new HeaderResult<PartitionKey>(string.Empty, false);
    }

    private static HeaderResult<string> ReadDeduplicationId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageDeduplicationId, out var value))
        {
            //we have an arn, and we want the topic
            return new HeaderResult<string>(value, true);
        }

        return new HeaderResult<string>(string.Empty, false);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to create message from Aws Sqs message")]
        public static partial void FailedToCreateMessageFromAwsSqsMessage(ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Warning, "Failed while deserializing Sqs Message body")]
        public static partial void FailedWhileDeserializingSqsMessageBody(ILogger logger, Exception ex);
        
        [LoggerMessage(LogLevel.Warning, "Failed to parse Sqs Message Body to valid Json Document")]
        public static partial void FailedToParseSqsMessageBodyToValidJsonDocument(ILogger logger, Exception ex);

    }
}
