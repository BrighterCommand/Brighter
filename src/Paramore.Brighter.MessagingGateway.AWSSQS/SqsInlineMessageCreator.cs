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
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

internal sealed partial class SqsInlineMessageCreator : SqsMessageCreatorBase, ISqsMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsInlineMessageCreator>();

    private Dictionary<string, JsonElement> _messageAttributes = new();

    public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
    {
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<Id>.Empty();

        try
        {
            var jsonDocument = JsonDocument.Parse(sqsMessage.Body);
            _messageAttributes = ReadMessageAttributes(jsonDocument);

            topic = ReadTopic();
            messageId = ReadMessageId();
            var cloudEvents = ReadMessageCloudEvents(); 
            var contentType = ReadContentType(cloudEvents);
            var correlationId = ReadCorrelationId();
            var handledCount = ReadHandledCount();
            var messageType = ReadMessageType();
            var timeStamp = ReadTimestamp(cloudEvents);
            var replyTo = ReadReplyTo();
            var subject = ReadMessageSubject(jsonDocument, cloudEvents);
            var receiptHandle = ReadReceiptHandle(sqsMessage);
            var partitionKey = ReadPartitionKey(sqsMessage);
            var deduplicationId = ReadDeduplicationId(sqsMessage);
            var type = ReadType(cloudEvents);
            var source = ReadSource(cloudEvents);
            var dataSchema = ReadDataSchema(cloudEvents);
            var specVersion = ReadSpecVersion(cloudEvents);
            var traceParent = ReadCloudEventsTraceParent(cloudEvents);
            var traceState = ReadCloudEventsTraceState(cloudEvents);
            var baggage = ReadCloudEventsBaggage(cloudEvents);
            
            var bag = ReadMessageBag();
            if (deduplicationId.Success)
            {
               bag[HeaderNames.DeduplicationId] = deduplicationId.Result;
            }

            if (receiptHandle.Success)
            {
                bag["ReceiptHandle"] = sqsMessage.ReceiptHandle;
            }

            var messageHeader = new MessageHeader(
                messageId: messageId.Result!,
                topic: topic.Result!,
                messageType: messageType.Result,
                source: source.Result,
                type: type.Result,
                timeStamp: timeStamp.Result,
                correlationId: correlationId.Result,
                replyTo: replyTo.Result,
                contentType: contentType.Result,
                handledCount: handledCount.Result,
                dataSchema: dataSchema.Result,
                subject: subject.Result,
                delayed: TimeSpan.Zero,
                partitionKey: partitionKey.Result,
                traceParent: traceParent.Result,
                traceState: traceState.Result
            )
            {
                SpecVersion = specVersion.Result!,
                Baggage = baggage.Result!,
                Bag = bag
            };

            return new Message(messageHeader, ReadMessageBody(jsonDocument));
        }
        catch (Exception e)
        {
            Log.FailedToCreateMessageFromAwsSqsMessage(s_logger, e);
            return Message.FailureMessage(topic.Result, messageId.Result);
        }
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

    private HeaderResult<ContentType> ReadContentType(Dictionary<string, string> headers)
    {
        if (_messageAttributes.TryGetValue(HeaderNames.DataContentType, out var contentType))
        {
            var result = contentType.GetValueInString();
            if (!string.IsNullOrEmpty(result))
            {
                return new HeaderResult<ContentType>(new ContentType(result), true);
            }
        }
        
        if (_messageAttributes.TryGetValue(HeaderNames.ContentType, out contentType))
        {
            var result = contentType.GetValueInString();
            if (!string.IsNullOrEmpty(result))
            {
                return new HeaderResult<ContentType>(new ContentType(result), true);
            }
        }
        
        if (headers.TryGetValue(HeaderNames.DataContentType, out var val))
        {
            return new HeaderResult<ContentType>(new ContentType(val), true);
        }

        return new HeaderResult<ContentType>(new ContentType(MediaTypeNames.Text.Plain), true);
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
    
    private Dictionary<string, string> ReadMessageCloudEvents()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.CloudEventHeaders, out var headerBag))
        {
            try
            {
                var json = headerBag.GetValueInString();
                if (string.IsNullOrEmpty(json))
                {
                    return new Dictionary<string, string>();
                }

                var bag = JsonSerializer.Deserialize<Dictionary<string, string>>(json!,
                    JsonSerialisationOptions.Options);

                return bag ?? new Dictionary<string, string>();
            }
            catch (Exception)
            {
                //suppress any errors in deserialization
            }
        }

        return new Dictionary<string, string>();
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

    private HeaderResult<string> ReadSpecVersion(Dictionary<string, string> headers)
    {
        if (_messageAttributes.TryGetValue(HeaderNames.SpecVersion, out var specVersion))
        {
            return new HeaderResult<string>(specVersion.GetValueInString(), true);
        }
        
        if (headers.TryGetValue(HeaderNames.SpecVersion, out var val))
        {
            return new HeaderResult<string>(val, true);
        }

        return new HeaderResult<string>(MessageHeader.DefaultSpecVersion, true);
    }

    private HeaderResult<CloudEventsType> ReadType(Dictionary<string, string> headers)
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Type, out var jsonElement))
        {
            var val = jsonElement.GetValueInString();
            if (!string.IsNullOrEmpty(val))
            {
                return new HeaderResult<CloudEventsType>(new CloudEventsType(val!), true);
            };
        }
        
        if (headers.TryGetValue(HeaderNames.Type, out var cloudEventType) 
            && !string.IsNullOrEmpty(cloudEventType))
        {
            return new HeaderResult<CloudEventsType>(new CloudEventsType(cloudEventType), true);
        }

        return new HeaderResult<CloudEventsType>(CloudEventsType.Empty, true);
    }
    
    private HeaderResult<Uri> ReadSource(Dictionary<string, string> headers)
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Source, out var source)
            && Uri.TryCreate(source.GetValueInString(), UriKind.RelativeOrAbsolute, out var uri))
        {
            return new HeaderResult<Uri>(uri, true);
        }
        
        if (headers.TryGetValue(HeaderNames.Source, out var val)
            && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out uri))
        {
            return new HeaderResult<Uri>(uri, true);
        }

        return new HeaderResult<Uri>(new Uri(MessageHeader.DefaultSource), true);
    }
    
     private HeaderResult<Uri?> ReadDataSchema(Dictionary<string, string> headers)
     {
         if (_messageAttributes.TryGetValue(HeaderNames.DataSchema, out var source)
             && Uri.TryCreate(source.GetValueInString(), UriKind.RelativeOrAbsolute, out var uri))
             
         {
             return new HeaderResult<Uri?>(uri, true);
         }
         
         if (headers.TryGetValue(HeaderNames.DataSchema, out var val)
             && Uri.TryCreate(val, UriKind.RelativeOrAbsolute, out uri))
         {
             return new HeaderResult<Uri?>(uri, true);
         }
    
         return new HeaderResult<Uri?>(null, true);
     }

    private HeaderResult<DateTimeOffset> ReadTimestamp(Dictionary<string, string> headers)
    {
         if (headers.TryGetValue(HeaderNames.Timestamp, out var val) 
            && DateTimeOffset.TryParse(val, out var value))
         { 
             return new HeaderResult<DateTimeOffset>(value, true); 
         }
        
         if (_messageAttributes.TryGetValue(HeaderNames.Time, out var timeStamp)
             && DateTimeOffset.TryParse(timeStamp.GetValueInString(), out value))
         {
             return new HeaderResult<DateTimeOffset>(value, true);
         }
         
         if (_messageAttributes.TryGetValue(HeaderNames.Timestamp, out timeStamp)
             && DateTimeOffset.TryParse(timeStamp.GetValueInString(), out value))
         {
             return new HeaderResult<DateTimeOffset>(value, true);
         }

         return new HeaderResult<DateTimeOffset>(DateTimeOffset.UtcNow, true);
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

        return new HeaderResult<Id?>(null, true);
    }

    private HeaderResult<Id> ReadMessageId()
    {
        if (_messageAttributes.TryGetValue(HeaderNames.Id, out var messageId))
        {
            var value = messageId.GetValueInString();
            if (!string.IsNullOrEmpty(value))
            {
                return new HeaderResult<Id>(Id.Create(value), true);
            }
        }

        return new HeaderResult<Id>(Id.Random(), true);
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

    private HeaderResult<string?> ReadMessageSubject(JsonDocument jsonDocument, Dictionary<string, string> headers)
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
              
              if (headers.TryGetValue(HeaderNames.Subject, out var subject))
              {
                  return new HeaderResult<string?>(subject, true);
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

        return new HeaderResult<PartitionKey>(PartitionKey.Empty, false);
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
    
    private static HeaderResult<TraceParent> ReadCloudEventsTraceParent(Dictionary<string,string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.TraceParent, out var value))
        {
            return new HeaderResult<TraceParent>(new TraceParent(value), true);
        }
        
        return new HeaderResult<TraceParent>(null, true);
    }
    
    private static HeaderResult<TraceState> ReadCloudEventsTraceState(Dictionary<string,string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.TraceState, out var value))
        {
            return new HeaderResult<TraceState>(new TraceState(value), true);
        }  
        return new HeaderResult<TraceState>(null, true);
    }
    
    private static HeaderResult<Baggage> ReadCloudEventsBaggage(Dictionary<string,string> cloudEventHeaders)
    {
        var baggage = new Baggage();
        if (cloudEventHeaders.TryGetValue(HeaderNames.Baggage, out var value))
        {
            baggage.LoadBaggage(value);
        }  
        
        return new HeaderResult<Baggage>(baggage, true);
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
