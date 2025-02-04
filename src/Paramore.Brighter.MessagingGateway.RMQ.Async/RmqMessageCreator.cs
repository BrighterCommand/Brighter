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
using System.Text;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

internal class RmqMessageCreator
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageCreator>();

    public Message CreateMessage(BasicDeliverEventArgs fromQueue)
    {
        var headers = fromQueue.BasicProperties.Headers ?? new Dictionary<string, object?>();
        var topic = HeaderResult<RoutingKey>.Empty();
        var messageId = HeaderResult<string>.Empty();
        var deliveryMode = fromQueue.BasicProperties.DeliveryMode;

        Message message;
        try
        {
            topic = ReadTopic(fromQueue, headers);
            messageId = ReadMessageId(fromQueue.BasicProperties.MessageId);
            HeaderResult<DateTime> timeStamp = ReadTimeStamp(fromQueue.BasicProperties);
            HeaderResult<int> handledCount = ReadHandledCount(headers);
            HeaderResult<TimeSpan> delay = ReadDelay(headers);
            HeaderResult<bool> redelivered = ReadRedeliveredFlag(fromQueue.Redelivered);
            HeaderResult<ulong> deliveryTag = ReadDeliveryTag(fromQueue.DeliveryTag);
            HeaderResult<MessageType> messageType = ReadMessageType(headers);
            HeaderResult<string?> replyTo = ReadReplyTo(fromQueue.BasicProperties);

            if (false == (topic.Success && messageId.Success && messageType.Success && timeStamp.Success && handledCount.Success))
            {
                message = FailureMessage(topic, messageId);
            }
            else
            {
                //TODO:CLOUD_EVENTS parse from headers
                    
                var messageHeader = new MessageHeader(
                    messageId: messageId.Result ?? string.Empty,
                    topic: topic.Result ?? RoutingKey.Empty,
                    messageType.Result,
                    source: null,
                    type: "",
                    timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                    correlationId: "",
                    replyTo: new RoutingKey(replyTo.Result ?? string.Empty),
                    contentType: "",
                    handledCount: handledCount.Result,
                    dataSchema: null,
                    subject: null,
                    delayed: delay.Result
                );

                //this effectively transfers ownership of our buffer 
                message = new Message(messageHeader, new MessageBody(fromQueue.Body, fromQueue.BasicProperties.Type ?? "plain/text"));

                headers.Each(header => message.Header.Bag.Add(header.Key, ParseHeaderValue(header.Value)));
            }

            if (headers.TryGetValue(HeaderNames.CORRELATION_ID, out object? correlationHeader))
            {
                var bytes = (byte[]?)correlationHeader; 
                if (bytes != null)
                {
                    var correlationId = Encoding.UTF8.GetString(bytes);
                    message.Header.CorrelationId = correlationId;
                }
            }

            message.DeliveryTag = deliveryTag.Result;
            message.Redelivered = redelivered.Result;
            message.Header.ReplyTo = replyTo.Result;
            message.Persist = deliveryMode == DeliveryModes.Persistent;
        }
        catch (Exception e)
        {
            s_logger.LogWarning(e,"Failed to create message from amqp message");
            message = FailureMessage(topic, messageId);
        }

        return message;
    }


    private HeaderResult<string?> ReadHeader(IDictionary<string, object?> dict, string key, bool dieOnMissing = false)
    {
        if (false == dict.TryGetValue(key, out object? value))
        {
            return new HeaderResult<string?>(string.Empty, !dieOnMissing);
        }

        if (!(value is byte[] bytes))
        {
            s_logger.LogWarning("The value of header {Key} could not be cast to a byte array", key);
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
            s_logger.LogWarning(e,"Failed to read the value of header {Key} as UTF-8, first 20 byes follow: \n\t{1}", key, firstTwentyBytes);
            return new HeaderResult<string?>(null, false);
        }
    }

    private Message FailureMessage(HeaderResult<RoutingKey?> topic, HeaderResult<string?> messageId)
    {
        var header = new MessageHeader(
            messageId.Success ? messageId.Result! : string.Empty,
            topic.Success ? topic.Result! : RoutingKey.Empty,
            MessageType.MT_UNACCEPTABLE);
        var message = new Message(header, new MessageBody(string.Empty));
        return message;
    }

    private static HeaderResult<ulong> ReadDeliveryTag(ulong deliveryTag)
    {
        return new HeaderResult<ulong>(deliveryTag, true);
    }

    private static HeaderResult<DateTime> ReadTimeStamp(IReadOnlyBasicProperties basicProperties)
    {
        if (basicProperties.IsTimestampPresent())
        {
            return new HeaderResult<DateTime>(UnixTimestamp.DateTimeFromUnixTimestampSeconds(basicProperties.Timestamp.UnixTime), true);
        }

        return new HeaderResult<DateTime>(DateTime.UtcNow, true);
    }

    private HeaderResult<MessageType> ReadMessageType(IDictionary<string, object?> headers)
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

    private HeaderResult<int> ReadHandledCount(IDictionary<string, object?> headers)
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

    private HeaderResult<TimeSpan> ReadDelay(IDictionary<string, object?> headers)
    {
        if (headers.ContainsKey(HeaderNames.DELAYED_MILLISECONDS) == false)
        {
            return new HeaderResult<TimeSpan>(TimeSpan.Zero, true);
        }

        int delayedMilliseconds;

        // on 32 bit systems the x-delay value will be a int and on 64 bit it will be a long, thank you erlang
        // The number will be negative after a message has been delayed
        // sticking with an int as you should not be delaying for more than 49 days
        switch (headers[HeaderNames.DELAYED_MILLISECONDS])
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

        return new HeaderResult<TimeSpan>(TimeSpan.FromMilliseconds( delayedMilliseconds), true);
    }

    private HeaderResult<RoutingKey?> ReadTopic(BasicDeliverEventArgs fromQueue, IDictionary<string, object?> headers)
    {
        return ReadHeader(headers, HeaderNames.TOPIC).Map(s =>
        {
            var val = string.IsNullOrEmpty(s) ? new RoutingKey(fromQueue.RoutingKey) : new RoutingKey(s!);
            return new HeaderResult<RoutingKey?>(val, true);
        });
    }

    private static HeaderResult<string?> ReadMessageId(string? messageId)
    {
        var newMessageId = Guid.NewGuid().ToString();

        if (string.IsNullOrEmpty(messageId))
        {
            s_logger.LogDebug("No message id found in message MessageId, new message id is {Id}", newMessageId);
            return new HeaderResult<string?>(newMessageId, true);
        }

        return new HeaderResult<string?>(messageId, true);
    }

    private static HeaderResult<bool> ReadRedeliveredFlag(bool redelivered)
    {
        return new HeaderResult<bool>(redelivered, true);
    }

    private static HeaderResult<string?> ReadReplyTo(IReadOnlyBasicProperties basicProperties)
    {
        if (basicProperties.IsReplyToPresent())
        {
            return new HeaderResult<string?>(basicProperties.ReplyTo!, true);
        }

        return new HeaderResult<string?>(null, true);
    }

    private static object ParseHeaderValue(object? value)
    {
        return (value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value) ?? string.Empty;
    }
}
