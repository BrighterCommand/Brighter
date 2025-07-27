#region Licence

/* The MIT License (MIT)
Copyright © 2014 Bob Gregory

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
using System.Linq;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

internal sealed partial class RmqMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageCreator>();

    public static Message CreateMessage(BasicDeliverEventArgs fromQueue)
    {
        var headers = fromQueue.BasicProperties.Headers ?? new Dictionary<string, object?>();
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<Id>.Empty();

        Message message;
        try
        {
            topic = ReadTopic(fromQueue, headers);
            messageId = ReadMessageId(fromQueue.BasicProperties.MessageId);

            var messageHeader = CreateMessageHeader(fromQueue, headers, topic, messageId);
            var bodyType = new ContentType(fromQueue.BasicProperties.Type ?? MediaTypeNames.Text.Plain); 
            message = new Message(messageHeader, new MessageBody(fromQueue.Body, bodyType));

            ProcessHeaderBag(headers, message);
            SetMessageMetadata(fromQueue, message);
        }
        catch (Exception e)
        {
            Log.FailedToCreateMessageFromAmqpMessage(s_logger, e);
            message = Message.FailureMessage(topic.Result, messageId.Result);
        }

        return message;
    }

    private static MessageHeader CreateMessageHeader(BasicDeliverEventArgs fromQueue, IDictionary<string, object?> headers,
        HeaderResult<RoutingKey?> topic, HeaderResult<Id?> messageId)
    {
        var timeStamp = ReadTimeStamp(fromQueue.BasicProperties);
        var handledCount = ReadHandledCount(headers);
        var delay = ReadDelay(headers);
        var messageType = ReadMessageType(headers);
        var replyTo = ReadReplyTo(fromQueue.BasicProperties);
        var correlationId = ReadCorrelationId(headers);
        var source = ReadSource(headers);
        var type = ReadType(headers);
        var dataSchema = ReadDataSchema(headers);
        var traceParent = ReadTraceParent(headers);
        var traceState = ReadTraceState(headers);
        var subject = ReadSubject(headers);

        var baggage = new Baggage();
        var baggageHeader = ReadBaggage(headers);
        if (baggageHeader.Success)
            baggage.LoadBaggage(baggageHeader.Result);

        if (false == (messageType.Success && timeStamp.Success && handledCount.Success))
            throw new InvalidOperationException("Required message header values are missing");
        
        var contentType = new ContentType(fromQueue.BasicProperties.ContentType ?? MediaTypeNames.Text.Plain); 

        return new MessageHeader(
            messageId: messageId.Result ?? string.Empty,
            topic: topic.Result ?? RoutingKey.Empty,
            messageType.Result,
            source: source.Result,
            type: type.Result,
            timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
            correlationId: correlationId.Result,
            replyTo: new RoutingKey(replyTo.Result ?? string.Empty),
            contentType: contentType,
            handledCount: handledCount.Result,
            dataSchema: dataSchema.Result,
            subject: subject.Result,
            delayed: delay.Result,
            traceParent: traceParent.Result,
            traceState: traceState.Result,
            baggage: baggage
        );
    }

    private static void ProcessHeaderBag(IDictionary<string, object?> headers, Message message)
    {
        headers.Each(header => message.Header.Bag.Add(header.Key, ParseHeaderValue(header.Value)));
    }

    private static void SetMessageMetadata(BasicDeliverEventArgs fromQueue, Message message)
    {
        var deliveryTag = ReadDeliveryTag(fromQueue.DeliveryTag);
        var redelivered = ReadRedeliveredFlag(fromQueue.Redelivered);

        message.DeliveryTag = deliveryTag.Result;
        message.Redelivered = redelivered.Result;
        message.Persist = fromQueue.BasicProperties.DeliveryMode == DeliveryModes.Persistent;
    }

    private static HeaderResult<string?> ReadHeader(IDictionary<string, object?> dict, string key, bool dieOnMissing = false)
    {
        if (false == dict.TryGetValue(key, out object? value))
        {
            return new HeaderResult<string?>(string.Empty, !dieOnMissing);
        }

        if (!(value is byte[] bytes))
        {
            Log.HeaderValueCouldNotBeCastToByteArray(s_logger, key);
            return new HeaderResult<string?>(null, false);
        }

        try
        {
            var val = Encoding.UTF8.GetString(bytes);
            return new HeaderResult<string?>(val, true);
        }
        catch (Exception e)
        {
            var firstTwentyBytes = BitConverter.ToString(bytes.Take(20).ToArray());
            Log.FailedToReadHeaderValueAsUtf8(s_logger, key, firstTwentyBytes, e);
            return new HeaderResult<string?>(null, false);
        }
    }

    private static HeaderResult<Id?> ReadCorrelationId(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CORRELATION_ID, out object? correlationHeader))
        {
            var correlationId = Encoding.UTF8.GetString((byte[])correlationHeader!);
            return new HeaderResult<Id?>(new Id(correlationId), true);
        }

        return new HeaderResult<Id?>(null, false);
    }

    private static HeaderResult<ulong> ReadDeliveryTag(ulong deliveryTag)
    {
        return new HeaderResult<ulong>(deliveryTag, true);
    }

    private static HeaderResult<DateTimeOffset> ReadTimeStamp(IReadOnlyBasicProperties basicProperties)
    {
        if (basicProperties.IsTimestampPresent())
        {
            return new HeaderResult<DateTimeOffset>(UnixTimestamp.DateTimeFromUnixTimestampSeconds(basicProperties.Timestamp.UnixTime), true);
        }

        if (basicProperties.Headers != null
            && basicProperties.Headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TIME, out var val)
            && val is byte[] bytes
            && DateTimeOffset.TryParse(Encoding.UTF8.GetString(bytes), out var dt))
        {
            return new HeaderResult<DateTimeOffset>(dt, true);
        }

        return new HeaderResult<DateTimeOffset>(DateTimeOffset.UtcNow, true);
    }

    private static HeaderResult<MessageType> ReadMessageType(IDictionary<string, object?> headers)
    {
        return ReadHeader(headers, HeaderNames.MESSAGE_TYPE)
            .Map(s =>
            {
                if (string.IsNullOrEmpty(s))
                {
                    return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
                }

                var success = Enum.TryParse(s, true, out MessageType result);
                return new HeaderResult<MessageType>(result, success);
            });
    }

    private static HeaderResult<int> ReadHandledCount(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.HANDLED_COUNT, out object? header) == false)
        {
            return new HeaderResult<int>(0, true);
        }

        switch (header)
        {
            case byte[] value:
            {
                var val = int.TryParse(Encoding.UTF8.GetString(value), out var handledCount) ? handledCount : 0;
                return new HeaderResult<int>(val, true);
            }
            case int value:
                return new HeaderResult<int>(value, true);
            default:
                return new HeaderResult<int>(0, true);
        }
    }

    private static HeaderResult<TimeSpan> ReadDelay(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.DELAYED_MILLISECONDS, out var delayedMsHeader) == false)
        {
            return new HeaderResult<TimeSpan>(TimeSpan.Zero, true);
        }

        int delayedMilliseconds;

        // on 32 bit systems the x-delay value will be a int and on 64 bit it will be a long, thank you erlang
        // The number will be negative after a message has been delayed
        // sticking with an int as you should not be delaying for more than 49 days
        switch (delayedMsHeader)
        {
            case byte[] value:
            {
                if (!int.TryParse(Encoding.UTF8.GetString(value), out var handledCount))
                    delayedMilliseconds = 0;
                else
                {
                    if (handledCount < 0)
                        handledCount = Math.Abs(handledCount);
                    delayedMilliseconds = handledCount;
                }

                break;
            }
            case int value:
            {
                if (value < 0)
                    value = Math.Abs(value);

                delayedMilliseconds = value;
                break;
            }
            case long value:
            {
                if (value < 0)
                    value = Math.Abs(value);

                delayedMilliseconds = (int)value;
                break;
            }
            default:
                return new HeaderResult<TimeSpan>(TimeSpan.Zero, false);
        }

        return new HeaderResult<TimeSpan>(TimeSpan.FromMilliseconds(delayedMilliseconds), true);
    }

    private static HeaderResult<RoutingKey?> ReadTopic(BasicDeliverEventArgs fromQueue, IDictionary<string, object?> headers)
    {
        var res = ReadHeader(headers, HeaderNames.TOPIC).Map(s =>
        {
            var val = string.IsNullOrEmpty(s) ? new RoutingKey(fromQueue.RoutingKey) : new RoutingKey(s ?? string.Empty);
            return new HeaderResult<RoutingKey?>(val, true);
        });

        if (res.Success)
        {
            return res;
        }
        
        return new HeaderResult<RoutingKey?>(new RoutingKey(fromQueue.RoutingKey), true);
    }

    private static HeaderResult<Id?> ReadMessageId(string? messageId)
    {
        if (string.IsNullOrEmpty(messageId))
        {
            var newMessageId = Id.Random();
            Log.NoMessageIdFoundInMessage(s_logger, newMessageId);
            return new HeaderResult<Id?>(newMessageId, true);
        }

        return new HeaderResult<Id?>(Id.Create(messageId), true);
    }

    private static HeaderResult<bool> ReadRedeliveredFlag(bool redelivered)
    {
        return new HeaderResult<bool>(redelivered, true);
    }

    private static HeaderResult<RoutingKey?> ReadReplyTo(IReadOnlyBasicProperties basicProperties)
    {
        if (basicProperties.IsReplyToPresent())
        {
            return new HeaderResult<RoutingKey?>(new RoutingKey(basicProperties.ReplyTo!), true);
        }

        return new HeaderResult<RoutingKey?>(null, true);
    }

    private static HeaderResult<Uri> ReadSource(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_SOURCE, out var source)
            && source is byte[] val
            && Uri.TryCreate(Encoding.UTF8.GetString(val), UriKind.RelativeOrAbsolute, out var uri))
        {
            return new HeaderResult<Uri>(uri, true);
        }

        return new HeaderResult<Uri>(new Uri(MessageHeader.DefaultSource), true);
    }

    private static HeaderResult<CloudEventsType> ReadType(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TYPE, out var type)
            && type is byte[] typeArray)
        {
            return new HeaderResult<CloudEventsType>(new CloudEventsType(Encoding.UTF8.GetString(typeArray)), true);
        }

        return new HeaderResult<CloudEventsType>(CloudEventsType.Empty, true);
    }

    private static HeaderResult<string?> ReadSubject(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_SUBJECT, out var subject)
            && subject is byte[] subjectArray)
        {
            return new HeaderResult<string?>(Encoding.UTF8.GetString(subjectArray), true);
        }

        return new HeaderResult<string?>(null, true);
    }

    private static HeaderResult<Uri?> ReadDataSchema(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, out var dataSchema)
            && dataSchema is byte[] dataSchemaArray
            && Uri.TryCreate(Encoding.UTF8.GetString(dataSchemaArray), UriKind.RelativeOrAbsolute, out var uri))
        {
            return new HeaderResult<Uri?>(uri, true);
        }

        return new HeaderResult<Uri?>(null, true);
    }

    private static HeaderResult<TraceParent?> ReadTraceParent(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, out var traceParent)
            && traceParent is byte[] traceParentArray)
        {
            return new HeaderResult<TraceParent?>(Encoding.UTF8.GetString(traceParentArray), true);
        }

        return new HeaderResult<TraceParent?>(string.Empty, true);
    }

    private static HeaderResult<TraceState?> ReadTraceState(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TRACE_STATE, out var traceState)
            && traceState is byte[] traceParentArray)
        {
            return new HeaderResult<TraceState?>(Encoding.UTF8.GetString(traceParentArray), true);
        }

        return new HeaderResult<TraceState?>(string.Empty, true);
    }

    private static HeaderResult<string?> ReadBaggage(IDictionary<string, object?> headers)
    {
        if (headers.TryGetValue(HeaderNames.W3C_BAGGAGE, out var traceParent)
            && traceParent is byte[] traceParentArray)
        {
            return new HeaderResult<string?>(Encoding.UTF8.GetString(traceParentArray), true);
        }

        return new HeaderResult<string?>(string.Empty, true);
    }

    private static object ParseHeaderValue(object? value)
    {
        if (value == null)
            return string.Empty;

        return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value;
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Warning, "Failed to create message from amqp message")]
        public static partial void FailedToCreateMessageFromAmqpMessage(ILogger logger, Exception e);

        [LoggerMessage(LogLevel.Warning, "The value of header {Key} could not be cast to a byte array")]
        public static partial void HeaderValueCouldNotBeCastToByteArray(ILogger logger, string key);

        [LoggerMessage(LogLevel.Warning, "Failed to read the value of header {Key} as UTF-8, first 20 byes follow: \n\t{FirstTwentyBytes}")]
        public static partial void FailedToReadHeaderValueAsUtf8(ILogger logger, string key, string firstTwentyBytes, Exception e);

        [LoggerMessage(LogLevel.Debug, "No message id found in message MessageId, new message id is {Id}")]
        public static partial void NoMessageIdFoundInMessage(ILogger logger, string id);
    }
}
