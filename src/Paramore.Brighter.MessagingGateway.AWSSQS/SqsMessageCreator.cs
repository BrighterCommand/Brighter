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
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

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

internal partial class SqsMessageCreator : SqsMessageCreatorBase, ISqsMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<SqsMessageCreator>();

    public Message CreateMessage(Amazon.SQS.Model.Message sqsMessage)
    {
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<string?>.Empty();

        //TODO:CLOUD_EVENTS parse from headers

        Message message;
        try
        {
            topic = ReadTopic(sqsMessage);
            messageId = ReadMessageId(sqsMessage);
            var contentType = ReadContentType(sqsMessage);
            var correlationId = ReadCorrelationId(sqsMessage);
            var handledCount = ReadHandledCount(sqsMessage);
            var messageType = ReadMessageType(sqsMessage);
            var timeStamp = ReadTimestamp(sqsMessage);
            var replyTo = ReadReplyTo(sqsMessage);
            var receiptHandle = ReadReceiptHandle(sqsMessage);
            var partitionKey = ReadPartitionKey(sqsMessage);
            var deduplicationId = ReadDeduplicationId(sqsMessage);
            var subject = ReadSubject(sqsMessage);

            var bodyType = (contentType.Success ? contentType.Result : "plain/text");

            var messageHeader = new MessageHeader(
                messageId: messageId.Result ?? string.Empty,
                topic: topic.Result ?? RoutingKey.Empty,
                messageType.Result,
                source: null,
                type: string.Empty,
                timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                correlationId: correlationId.Success ? correlationId.Result : string.Empty,
                replyTo: replyTo.Success ? new RoutingKey(replyTo.Result!) : RoutingKey.Empty,
                contentType: bodyType!,
                handledCount: handledCount.Result,
                dataSchema: null,
                subject: subject.Success ? subject.Result : string.Empty,
                delayed: TimeSpan.Zero,
                partitionKey: partitionKey.Success ? partitionKey.Result : string.Empty
            );

            message = new Message(messageHeader, ReadMessageBody(sqsMessage, bodyType!));

            //deserialize the bag 
            var bag = ReadMessageBag(sqsMessage);
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
                message.Header.Bag.Add("ReceiptHandle", receiptHandle.Result);
            }
        }
        catch (Exception e)
        {
            Log.FailedToCreateMessageFromAmqpMessage(s_logger, e);
            message = FailureMessage(topic, messageId);
        }

        return message;
    }

    private static MessageBody ReadMessageBody(Amazon.SQS.Model.Message sqsMessage, string contentType)
    {
        if (contentType == CompressPayloadTransformerAsync.GZIP
            || contentType == CompressPayloadTransformerAsync.DEFLATE
            || contentType == CompressPayloadTransformerAsync.BROTLI)
            return new MessageBody(sqsMessage.Body, contentType, CharacterEncoding.Base64);

        return new MessageBody(sqsMessage.Body, contentType);
    }

    private static Dictionary<string, object> ReadMessageBag(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Bag, out MessageAttributeValue? value))
        {
            try
            {
                var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(value.StringValue,
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

    private static HeaderResult<string> ReadReplyTo(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.ReplyTo, out MessageAttributeValue? value))
        {
            return new HeaderResult<string>(value.StringValue, true);
        }

        return new HeaderResult<string>(string.Empty, true);
    }

    private static HeaderResult<DateTime> ReadTimestamp(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Timestamp, out MessageAttributeValue? value))
        {
            if (DateTime.TryParse(value.StringValue, out DateTime timestamp))
            {
                return new HeaderResult<DateTime>(timestamp, true);
            }
        }

        return new HeaderResult<DateTime>(DateTime.UtcNow, true);
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

    private static HeaderResult<int> ReadHandledCount(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.HandledCount, out MessageAttributeValue? value))
        {
            if (int.TryParse(value.StringValue, out int handledCount))
            {
                return new HeaderResult<int>(handledCount, true);
            }
        }

        return new HeaderResult<int>(0, true);
    }

    private static HeaderResult<string> ReadCorrelationId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.CorrelationId,
                out MessageAttributeValue? correlationId))
        {
            return new HeaderResult<string>(correlationId.StringValue, true);
        }

        return new HeaderResult<string>(string.Empty, true);
    }

    private static HeaderResult<string> ReadContentType(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.ContentType, out MessageAttributeValue? value))
        {
            return new HeaderResult<string>(value.StringValue, true);
        }

        return new HeaderResult<string>(string.Empty, true);
    }

    private static HeaderResult<string?> ReadMessageId(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.MessageAttributes.TryGetValue(HeaderNames.Id, out MessageAttributeValue? value))
        {
            return new HeaderResult<string?>(value.StringValue, true);
        }

        return new HeaderResult<string?>(string.Empty, true);
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

    private static HeaderResult<string> ReadPartitionKey(Amazon.SQS.Model.Message sqsMessage)
    {
        if (sqsMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var value))
        {
            //we have an arn, and we want the topic
            var messageGroupId = value;
            return new HeaderResult<string>(messageGroupId, true);
        }

        return new HeaderResult<string>(null, false);
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
            //we have an arn, and we want the topic
            var subject = value.StringValue;
            return new HeaderResult<string>(subject, true);
        }

        return new HeaderResult<string>(null, false);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to create message from amqp message")]
        public static partial void FailedToCreateMessageFromAmqpMessage(ILogger logger, Exception e);
    }
}
