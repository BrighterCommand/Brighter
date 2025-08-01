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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
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

        private readonly IModel _channel;
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
        public RmqMessagePublisher(IModel channel, RmqMessagingGatewayConnection connection) 
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
       }

       /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delay">The delay in ms. 0 is no delay. Defaults to 0</param>
        public void PublishMessage(Message message, TimeSpan? delay = null)
        {
            if (_connection.Exchange is null)
                throw new InvalidOperationException("RmqMessagePublisher.PublishMessage: Connections Exchange is null");

            var deliveryTag = message.Header.Bag.ContainsKey(HeaderNames.DELIVERY_TAG) ? message.DeliveryTag.ToString() : null;

            Dictionary<string, object> headers = AddCloudEventHeaders(message);

            AddUserDefinedHeaders(message, delay, headers, deliveryTag);
            
            AddDeliveryHeaders(delay, headers, deliveryTag);

            var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
            var bodyContentType = message.Body.ContentType ?? contentType; 
            
            _channel.BasicPublish(
                _connection.Exchange.Name,
                message.Header.Topic,
                false,
                CreateBasicProperties(
                    message.Id, 
                    message.Header.TimeStamp, 
                    bodyContentType.ToString(),
                    contentType.ToString(), 
                    message.Header.ReplyTo ?? string.Empty,
                    message.Persist,
                    headers),
                message.Body.Bytes);
        }

        /// <summary>
        /// Requeues the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="timeOut">Delay. Set to TimeSpan.Zero for not delay</param>
        public void RequeueMessage(Message message, ChannelName queueName, TimeSpan timeOut)
        {
            var messageId = Uuid.NewAsString();
            const string deliveryTag = "1";

            Log.RequeueMessageInformation(s_logger, message.Id, deliveryTag, messageId, 1);

            Dictionary<string, object> headers = AddCloudEventHeaders(message);

            AddUserDefinedHeaders(message, timeOut, headers, deliveryTag);
            
            AddDeliveryHeaders(TimeSpan.Zero, headers, deliveryTag);

            AddOriginalMessageIdOnRepublish(message, headers);
            
            var contentType = message.Header.ContentType ?? new ContentType(MediaTypeNames.Text.Plain);
            var bodyContentType = message.Body.ContentType ?? contentType;

            // To send it to the right queue use the default (empty) exchange
            _channel.BasicPublish(
                string.Empty,
                queueName.Value,
                false,
                CreateBasicProperties(
                    messageId, 
                    message.Header.TimeStamp, 
                    bodyContentType.ToString(),
                    contentType.ToString(), 
                    message.Header.ReplyTo ?? string.Empty,
                    message.Persist,
                    headers),
                message.Body.Bytes);
        }
 
        private static Dictionary<string, object> AddCloudEventHeaders(Message message)
        {
            var headers = new Dictionary<string, object>
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
                headers.Add(HeaderNames.CLOUD_EVENTS_SUBJECT, message.Header.Subject!);
        
            if (message.Header.DataSchema != null)
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, message.Header.DataSchema.ToString());

            if (message.Header.CorrelationId != string.Empty)
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId?.Value!);
            
            if (!string.IsNullOrEmpty(message.Header.TraceParent?.Value))
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, message.Header.TraceParent?.Value!);
            
            if (!string.IsNullOrEmpty(message.Header.TraceState?.Value))
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_STATE, message.Header.TraceState?.Value!);
            
            if (message.Header.Baggage.Any())
                headers.Add(HeaderNames.W3C_BAGGAGE, message.Header.Baggage.ToString());
            return headers;
        }
        
        private static void AddOriginalMessageIdOnRepublish(Message message, Dictionary<string, object> headers)
        {
            if (!message.Header.Bag.Any(h => h.Key.Equals(HeaderNames.ORIGINAL_MESSAGE_ID, StringComparison.CurrentCultureIgnoreCase)))
                headers.Add(HeaderNames.ORIGINAL_MESSAGE_ID, message.Id.Value);
        }
        
        private static void AddUserDefinedHeaders(Message message, TimeSpan? delay, Dictionary<string, object> headers, string? deliveryTag)
        {
            message.Header.Bag.Each(header =>
            {
                if (!_headersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
            });

        }

        private static void AddDeliveryHeaders(TimeSpan? delay, Dictionary<string, object> headers, string? deliveryTag)
        {
            if (!string.IsNullOrEmpty(deliveryTag))
                headers.Add(HeaderNames.DELIVERY_TAG, deliveryTag!);

            if (delay > TimeSpan.Zero)
                headers.Add(HeaderNames.DELAY_MILLISECONDS, delay.Value.TotalMilliseconds);
        }

        private IBasicProperties CreateBasicProperties(string id, DateTimeOffset timeStamp, string type, string contentType,
            string replyTo, bool persistMessage, IDictionary<string, object>? headers = null)
        {
            var basicProperties = _channel.CreateBasicProperties();

            basicProperties.DeliveryMode = (byte) (persistMessage ? 2 : 1); // delivery mode set to 2 if message is persistent or 1 if non-persistent
            basicProperties.ContentType = contentType;
            basicProperties.Type = type;
            basicProperties.MessageId = id;
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(timeStamp.DateTime));
            if (!string.IsNullOrEmpty(replyTo))
                basicProperties.ReplyTo = replyTo;

            if (!(headers is null))
            {
                foreach (var header in headers)
                {
                    if(!IsAnAmqpType(header.Value))
                        throw new ConfigurationException($"The value {header.Value} is type {header.Value.GetType()} for header {header.Key} value only supports the AMQP 0-8/0-9 standard entry types S, I, D, T and F, as well as the QPid-0-8 specific b, d, f, l, s, t, x and V types and the AMQP 0-9-1 A type.");
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
                null or string _ or byte[] _ or int _ or uint _ or decimal _ or AmqpTimestamp _ or IDictionary _
                    or IList _ or byte _ or sbyte _ or double _ or float _ or long _ or short _ or bool _  => true,
                _ => false
            };
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "RmqMessagePublisher: Regenerating message {OldMessageId} with DeliveryTag of {OldDeliveryTag} to {MessageId} with DeliveryTag of {DeliveryTag}")]
            public static partial void RequeueMessageInformation(ILogger logger, string oldMessageId, string oldDeliveryTag, string messageId, int deliveryTag);
        }
    }
}

