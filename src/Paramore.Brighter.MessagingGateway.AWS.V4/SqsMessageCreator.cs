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
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.MessagingGateway.AWS.V4;

//arn:aws:sns:us-east-1:123456789012:my_corporate_topic:02034b43-fefa-4e07-a5eb-3be56f8c54ce
//https://docs.aws.amazon.com/general/latest/gr/aws-arns-and-namespaces.html#genref-arns
internal enum ARNAmazonSNS
{
    Arn = 0,
    Aws = 1,
    Sns = 2,
    Region = 3,
    AccountId = 4,
    TopicName = 5,
    SubscriptionId = 6
}

internal sealed partial class SqsMessageCreator : SqsMessageCreatorBase, ISqsMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsMessageCreator>();

    public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
    {
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<Id?>.Empty();

        Message message;
        try
        {
            topic = ReadTopic(sqsMessage);
            messageId = ReadMessageId(sqsMessage);
            var contentType = ReadContentType(sqsMessage);
            var correlationId = ReadCorrelationId(sqsMessage);
            var messageType = ReadMessageType(sqsMessage);
            var timeStamp = ReadTimestamp(sqsMessage);
            var replyTo = ReadReplyTo(sqsMessage);
            var receiptHandle = ReadReceiptHandle(sqsMessage);
            var partitionKey = ReadPartitionKey(sqsMessage);
            var deduplicationId = ReadDeduplicationId(sqsMessage);
            var subject = ReadSubject(sqsMessage);
            var cloudEventHeaders = ReadCloudEventHeaders(sqsMessage);
            var bag = ReadMessageBag(sqsMessage);
            var handledCount = ReadHandledCount(bag);
            var cloudEventsTimeStamp = ReadCloudEventsTimeStamp(cloudEventHeaders);
            var source = ReadCloudEventSource(cloudEventHeaders);
            var type = ReadCloudEventType(cloudEventHeaders);
            var dataSchema = ReadCloudEventsDataSchema(cloudEventHeaders);
            var specVersion = ReadCloudEventsSpecVersion(cloudEventHeaders);

            var bodyType = contentType.Success ? contentType.Result : new ContentType(MediaTypeNames.Text.Plain);

            var messageHeader = new MessageHeader(
                messageId: messageId.Result ?? Id.Empty,
                topic: topic.Result ?? RoutingKey.Empty,
                messageType.Result,
                source:  source.Result,
                type: type.Result,
                timeStamp: cloudEventsTimeStamp.Success ? cloudEventsTimeStamp.Result : timeStamp.Result,
                correlationId: correlationId.Success ? correlationId.Result : Id.Empty,
                replyTo: replyTo.Result is not null ? new RoutingKey(replyTo.Result) : RoutingKey.Empty,
                contentType: bodyType!,
                handledCount: handledCount.Result,
                dataSchema: dataSchema.Result,
                subject: subject.Result,
                delayed: TimeSpan.Zero,
                partitionKey: partitionKey.Success ? partitionKey.Result : string.Empty
            )
            {
                SpecVersion = specVersion.Result!
            };

            message = new Message(messageHeader, ReadMessageBody(sqsMessage, bodyType!));

            PopulateBag(bag, message, deduplicationId, receiptHandle);
        }
        catch (Exception e)
        {
            Log.FailedToCreateMessageFromAmqpMessage(s_logger, e);
            message = Message.FailureMessage(topic.Result, messageId.Success ? messageId.Result : Id.Empty);
        }

        return message;
    }

    private static void PopulateBag(Dictionary<string, object> bag, Message message, HeaderResult<string> deduplicationId, HeaderResult<string> receiptHandle)
    {
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
            message.Header.Bag.Add("ReceiptHandle", receiptHandle.Result);
        }
    }

    private static HeaderResult<Uri?> ReadCloudEventsDataSchema(Dictionary<string, string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.DataSchema, out var value))
        {
            if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
            {
                return new HeaderResult<Uri?>(uri, true);
            }
        }

        return new HeaderResult<Uri?>(null, false);
    }
    
    private static Dictionary<string, string> ReadCloudEventHeaders(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.CloudEventHeaders, out var value))
        {
            try
            {
                var cloudEventHeaders = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(value.StringValue,
                    JsonSerialisationOptions.Options);
                if (cloudEventHeaders != null)
                    return cloudEventHeaders;
            }
            catch (Exception)
            {
                //we weill just suppress conversion errors, and return an empty bag
            }        }

        return new Dictionary<string, string>();
    }
    
    private static HeaderResult<Uri?> ReadCloudEventSource(Dictionary<string, string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.Source, out var value))
        {
            if (Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri))
            {
                return new HeaderResult<Uri?>(uri, true);
            }
        }

        return new HeaderResult<Uri?>(null, false);
    }
    
    private static HeaderResult<string> ReadCloudEventsSpecVersion(Dictionary<string,string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.SpecVersion, out var value))
        {
            return new HeaderResult<string>(value, true);
        }  
        return new HeaderResult<string>(MessageHeader.DefaultSpecVersion, true);
    }
    
    private static HeaderResult<DateTimeOffset?> ReadCloudEventsTimeStamp(Dictionary<string, string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.Timestamp, out var value))
        {
            if (DateTime.TryParse(value, out var dateTime))
            {
                return new HeaderResult<DateTimeOffset?>(dateTime, true);
            }
        }
        
        return new HeaderResult<DateTimeOffset?>(null, false);
    }
    
    private static HeaderResult<CloudEventsType?> ReadCloudEventType(Dictionary<string, string> cloudEventHeaders)
    {
        if (cloudEventHeaders.TryGetValue(HeaderNames.Type, out var value))
        {
            return new HeaderResult<CloudEventsType?>(new CloudEventsType(value), true);
        }

        return new HeaderResult<CloudEventsType?>(CloudEventsType.Empty, false);
    }


    private static MessageBody ReadMessageBody(Amazon.SQS.Model.Message sqsMessage, ContentType contentType)
    {
        if (contentType.ToString() == CompressPayloadTransformer.GZIP
            || contentType.ToString() == CompressPayloadTransformer.DEFLATE
            || contentType.ToString() == CompressPayloadTransformer.BROTLI)
            return new MessageBody(sqsMessage.Body, contentType, CharacterEncoding.Base64);

        return new MessageBody(sqsMessage.Body, contentType);
    }

    private static Dictionary<string, object> ReadMessageBag(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Bag, out MessageAttributeValue? value))
        {
            try
            {
                var bag = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(value.StringValue,
                    JsonSerialisationOptions.Options);
                if (bag != null)
                    return bag;
            }
            catch (Exception)
            {
                //we weill just suppress conversion errors, and return an empty bag
            }
        }

        return new Dictionary<string, object>();
    }

    private static HeaderResult<RoutingKey> ReadReplyTo(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.ReplyTo, out MessageAttributeValue? value))
        {
            return new HeaderResult<RoutingKey>(new RoutingKey(value.StringValue), true);
        }

        return new HeaderResult<RoutingKey>(RoutingKey.Empty, true);
    }

    private static HeaderResult<DateTimeOffset> ReadTimestamp(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Time, out var value)
            && DateTimeOffset.TryParse(value.StringValue, out var timestamp))
        {
            return new HeaderResult<DateTimeOffset>(timestamp, true);
        }
        
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Timestamp, out value)
            && DateTimeOffset.TryParse(value.StringValue, out timestamp))
        {
            return new HeaderResult<DateTimeOffset>(timestamp, true);
        }

        return new HeaderResult<DateTimeOffset>(DateTimeOffset.UtcNow, true);
    }

    private static HeaderResult<MessageType> ReadMessageType(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.MessageType, out MessageAttributeValue? value))
        {
            if (Enum.TryParse(value.StringValue, out MessageType messageType))
            {
                return new HeaderResult<MessageType>(messageType, true);
            }
        }

        return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
    }

    private static HeaderResult<int> ReadHandledCount(Dictionary<string, object> bag)
    {
        if (bag.TryGetValue(HeaderNames.HandledCount, out var value))
        {
            int handledCount = Convert.ToInt32(value);
            return new HeaderResult<int>(handledCount, true);
        }

        return new HeaderResult<int>(0, true);
    }

    private static HeaderResult<Id> ReadCorrelationId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.CorrelationId,
                out MessageAttributeValue? correlationId))
        {
            return new HeaderResult<Id>(new Id(correlationId.StringValue), true);
        }

        return new HeaderResult<Id>(Id.Empty, true);
    }

    private static HeaderResult<ContentType> ReadContentType(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.DataContentType, out var value))
        {
            if (!string.IsNullOrEmpty(value.StringValue))
                return new HeaderResult<ContentType>(new ContentType(value.StringValue), true);
            else
                return new HeaderResult<ContentType>(new ContentType(MediaTypeNames.Text.Plain), true);
        }
        
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.ContentType, out value))
        {
            return new HeaderResult<ContentType>(new ContentType(value.StringValue), true);
        }

        return new HeaderResult<ContentType>(new ContentType(MediaTypeNames.Text.Plain), true);
    }

    private static HeaderResult<Id?> ReadMessageId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Id, out MessageAttributeValue? messageId))
        {
            var value = messageId.StringValue;
            return new HeaderResult<Id?>(string.IsNullOrEmpty(value) ? Id.Random() : Id.Create(value), true);
        }

        return new HeaderResult<Id?>(Id.Random(), true);
    }

    private static HeaderResult<RoutingKey> ReadTopic(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Topic, out MessageAttributeValue? value))
        {
            //we have an arn, and we want the topic
            var topic = value.StringValue ?? string.Empty;
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

    private static HeaderResult<PartitionKey> ReadPartitionKey(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var value))
        {
            //we have an arn, and we want the topic
            var messageGroupId = value;
            return new HeaderResult<PartitionKey>(messageGroupId, true);
        }

        return new HeaderResult<PartitionKey>(null, false);
    }

    private static HeaderResult<string> ReadDeduplicationId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageDeduplicationId, out var value))
        {
            //we have an arn, and we want the topic
            var messageGroupId = value;
            return new HeaderResult<string>(messageGroupId, true);
        }

        return new HeaderResult<string>(null, false);
    }
    
    private static HeaderResult<string> ReadSubject(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Subject, out var value))
        {
            return new HeaderResult<string>(value.StringValue, true);
        }

        return new HeaderResult<string>(null, false);
    }
   
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to create message from amqp message")]
        public static partial void FailedToCreateMessageFromAmqpMessage(ILogger logger, Exception e);
    }
}
