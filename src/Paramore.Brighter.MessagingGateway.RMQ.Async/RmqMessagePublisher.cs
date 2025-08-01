#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

/// <summary>
/// Class RmqMessagePublisher.
/// </summary>
internal sealed partial class RmqMessagePublisher
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessagePublisher>();

    private static readonly string[] _headersToReset =
    [
        HeaderNames.DELAY_MILLISECONDS,
        HeaderNames.MESSAGE_TYPE,
        HeaderNames.TOPIC,
        HeaderNames.HANDLED_COUNT,
        HeaderNames.DELIVERY_TAG,
        HeaderNames.CORRELATION_ID
    ];

    private readonly IChannel _channel;
    private readonly RmqMessagingGatewayConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="RmqMessagePublisher"/> class.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="connection">The exchange we want to talk to.</param>
    /// <exception cref="System.ArgumentNullException">
    /// channel
    /// or
    /// exchangeName
    /// </exception>
    public RmqMessagePublisher(IChannel channel, RmqMessagingGatewayConnection connection)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        _connection = connection;

        _channel = channel;
    }

    /// <summary>
    /// Publishes the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">The delay in ms. 0 is no delay. Defaults to 0</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that cancels the Publish operation</param>
    public async Task PublishMessageAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        if (_connection.Exchange is null)
            throw new InvalidOperationException("RMQMessagingGateway: No Exchange specified");

        var messageId = message.Id;
        var deliveryTag = message.Header.Bag.ContainsKey(HeaderNames.DELIVERY_TAG) ? message.DeliveryTag.ToString() : null;

        Dictionary<string, object?> headers = AddCloudEventsHeaders(message);

        AddUserDefinedHeaders(message, headers);

        AddDeliveryHeaders(delay, deliveryTag, headers);

        await _channel.BasicPublishAsync(
            _connection.Exchange.Name,
            message.Header.Topic,
            false,
            CreateBasicProperties(
                messageId,
                message.Header.TimeStamp,
                message.Body.ContentType is not null ? message.Body.ContentType.ToString() : MediaTypeNames.Text.Plain,
                message.Header.ContentType is not null ? message.Header.ContentType.ToString() : MediaTypeNames.Text.Plain,
                message.Header.ReplyTo ?? string.Empty,
                message.Persist,
                headers),
            message.Body.Bytes, cancellationToken);
    }

    /// <summary>
    /// Requeues the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="queueName">The queue name.</param>
    /// <param name="timeOut">Delay. Set to TimeSpan.Zero for not delay</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that cancels the requeue</param>
    public async Task RequeueMessageAsync(Message message, ChannelName queueName, TimeSpan timeOut, CancellationToken cancellationToken = default)
    {
        var messageId = Uuid.NewAsString();
        const string deliveryTag = "1";

        Log.RegeneratingMessage(s_logger, message.Id, deliveryTag, messageId, 1);

        Dictionary<string, object?> headers = AddCloudEventsHeaders(message);

        AddUserDefinedHeaders(message, headers);

        AddDeliveryHeaders(TimeSpan.Zero, deliveryTag, headers);
        
        AddOriginalMessageIdOnRepublish(message, headers);

        // To send it to the right queue use the default (empty) exchange
        await _channel.BasicPublishAsync(
            string.Empty,
            queueName.Value,
            false,
            CreateBasicProperties(
                messageId,
                message.Header.TimeStamp,
                message.Body.ContentType is not null ? message.Body.ContentType.ToString() : MediaTypeNames.Text.Plain,
                message.Header.ContentType is not null ? message.Header.ContentType.ToString() : MediaTypeNames.Text.Plain,
                message.Header.ReplyTo ?? string.Empty,
                message.Persist,
                headers),
            message.Body.Bytes, cancellationToken);
    }

    private static Dictionary<string, object?> AddCloudEventsHeaders(Message message)
    {
        var headers = new Dictionary<string, object?>
        {
            // Cloud event
            [HeaderNames.CLOUD_EVENTS_ID] = message.Header.MessageId.Value,
            [HeaderNames.CLOUD_EVENTS_SPEC_VERSION] = message.Header.SpecVersion,
            [HeaderNames.CLOUD_EVENTS_TYPE] = message.Header.Type.Value,
            [HeaderNames.CLOUD_EVENTS_SOURCE] = message.Header.Source.ToString(),
            [HeaderNames.CLOUD_EVENTS_TIME] = message.Header.TimeStamp.ToRfc3339(),

            // Brighter custom headers
            [HeaderNames.MESSAGE_TYPE] = message.Header.MessageType.ToString(),
            [HeaderNames.TOPIC] = message.Header.Topic.Value,
            [HeaderNames.HANDLED_COUNT] = message.Header.HandledCount,
        };

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            headers.Add(HeaderNames.CLOUD_EVENTS_SUBJECT, message.Header.Subject);
        }

        if (message.Header.DataSchema != null)
        {
            headers.Add(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, message.Header.DataSchema.ToString());
        }

        if (message.Header.CorrelationId != string.Empty)
            headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.Value);
        
        if (!string.IsNullOrEmpty(message.Header.TraceParent?.Value))
            headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, message.Header.TraceParent?.Value!);
            
        if (!string.IsNullOrEmpty(message.Header.TraceState?.Value))
            headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_STATE, message.Header.TraceState?.Value!);
            
        if (message.Header.Baggage.Any())
            headers.Add(HeaderNames.W3C_BAGGAGE, message.Header.Baggage.ToString());
        return headers;
    }
    
    private static void AddDeliveryHeaders(TimeSpan? delay, string? deliveryTag, Dictionary<string, object?> headers)
    {
        if (!string.IsNullOrEmpty(deliveryTag))
            headers.Add(HeaderNames.DELIVERY_TAG, deliveryTag!);

        if (delay > TimeSpan.Zero)
            headers.Add(HeaderNames.DELAY_MILLISECONDS, delay.Value.TotalMilliseconds);
    }

    private static void AddOriginalMessageIdOnRepublish(Message message, Dictionary<string, object?> headers)
    {
        if (!message.Header.Bag.Any(h => h.Key.Equals(HeaderNames.ORIGINAL_MESSAGE_ID, StringComparison.CurrentCultureIgnoreCase)))
        {
            headers.Add(HeaderNames.ORIGINAL_MESSAGE_ID, message.Id.Value);
        }
    }

    private static void AddUserDefinedHeaders(Message message, Dictionary<string, object?> headers)
    {
        message.Header.Bag.Each(header =>
        {
            if (!_headersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
        });
    }

    private static BasicProperties CreateBasicProperties(
        string id,
        DateTimeOffset timeStamp,
        string type,
        string contentType,
        string replyTo,
        bool persistMessage,
        IDictionary<string, object?>? headers = null)
    {
        var basicProperties = new BasicProperties
        {
            DeliveryMode =
                persistMessage
                    ? DeliveryModes.Persistent
                    : DeliveryModes.Transient, // delivery mode set to 2 if message is persistent or 1 if non-persistent
            ContentType = contentType,
            Type = type,
            MessageId = id,
            Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(timeStamp.DateTime))
        };

        if (!string.IsNullOrEmpty(replyTo))
        {
            basicProperties.ReplyTo = replyTo;
        }

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                if (header.Value is not null && !IsAnAmqpType(header.Value))
                {
                    throw new ConfigurationException(
                        $"The value {header.Value} is type {header.Value.GetType()} for header {header.Key} value only supports the AMQP 0-8/0-9 standard entry types S, I, D, T and F, as well as the QPid-0-8 specific b, d, f, l, s, t, x and V types and the AMQP 0-9-1 A type.");
                }
            }

            basicProperties.Headers = headers;
        }

        return basicProperties;
    }

    /// <summary>
    /// Supports the AMQP 0-8/0-9 standard entry types S, I, D, T
    /// and F, as well as the QPid-0-8 specific b, d, f, l, s, t
    /// x and V types and the AMQP 0-9-1 A type.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool IsAnAmqpType(object value)
    {
        return value switch
        {
            null or string _ or byte[] _ or int _ or uint _ or decimal _ or AmqpTimestamp _ or IDictionary _ or IList _
                or byte _ or sbyte _ or double _ or float _ or long _ or short _ or bool _ => true,
            _ => false
        };
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "RmqMessagePublisher: Regenerating message {OldMessageId} with DeliveryTag of {OldDeliveryTag} to {MessageId} with DeliveryTag of {DeliveryTag}")]
        public static partial void RegeneratingMessage(ILogger logger, string oldMessageId, string oldDeliveryTag, string messageId, int deliveryTag);
    }
}
