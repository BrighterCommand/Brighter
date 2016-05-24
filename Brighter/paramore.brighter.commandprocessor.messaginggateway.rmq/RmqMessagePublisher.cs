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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using paramore.brighter.commandprocessor.extensions;
using paramore.brighter.commandprocessor.Logging;

using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RmqMessagePublisher.
    /// </summary>
    public class RmqMessagePublisher
    {
        private static string[] HeadersToReset = { HeaderNames.DELAY_MILLISECONDS, HeaderNames.MESSAGE_TYPE, HeaderNames.TOPIC, HeaderNames.HANDLED_COUNT, HeaderNames.DELIVERY_TAG, HeaderNames.CORRELATION_ID };

        private readonly IModel _channel;
        private readonly string _exchangeName;
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessagePublisher"/> class.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="exchangeName">Name of the exchange.</param>
        /// <exception cref="System.ArgumentNullException">
        /// channel
        /// or
        /// exchangeName
        /// </exception>
        public RmqMessagePublisher(IModel channel, string exchangeName) 
            : this(channel, exchangeName, LogProvider.GetCurrentClassLogger())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="RmqMessagePublisher"/> class.
        /// Use this if you need to inject the logger for tests
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="exchangeName">Name of the exchange.</param>
        /// <param name="logger">The logger to use</param>
        /// <exception cref="System.ArgumentNullException">
        /// channel
        /// or
        /// exchangeName
        /// </exception>
        public RmqMessagePublisher(IModel channel, string exchangeName, ILog logger)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }
            if (exchangeName == null)
            {
                throw new ArgumentNullException("exchangeName");
            }

            _channel = channel;
            _exchangeName = exchangeName;
            _logger = logger;
        }

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The delay in ms.</param>
        public void PublishMessage(Message message, int delayMilliseconds)
        {
            var messageId = message.Id;
            var deliveryTag = message.Header.Bag.ContainsKey(HeaderNames.DELIVERY_TAG) ? message.GetDeliveryTag().ToString() : null;

            var headers = new Dictionary<string, object>
            {
                {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                {HeaderNames.TOPIC, message.Header.Topic},
                {HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString(CultureInfo.InvariantCulture)}
            };

            if (message.Header.CorrelationId != Guid.Empty)
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.ToString());

            message.Header.Bag.Each(header =>
            {
                if (!HeadersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
            });

            if (!string.IsNullOrEmpty(deliveryTag))
                headers.Add(HeaderNames.DELIVERY_TAG, deliveryTag);

            if (delayMilliseconds > 0)
                headers.Add(HeaderNames.DELAY_MILLISECONDS, delayMilliseconds);

            _channel.BasicPublish(
                _exchangeName,
                message.Header.Topic,
                false,
                CreateBasicProperties(messageId, message.Header.TimeStamp, message.Body.BodyType,
                    message.Header.ContentType, headers),
                message.Header.ContentType == "text/plain"
                    ? Encoding.UTF8.GetBytes(message.Body.Value)
                    : message.Body.Bytes);
        }

        /// <summary>
        /// Requeues the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="queueName">The queue name.</param>
        /// <param name="delayMilliseconds">Delay in ms.</param>
        public void RequeueMessage(Message message, string queueName, int delayMilliseconds)
        {
            var messageId = Guid.NewGuid() ;
            const string DeliveryTag = "1";

            if (_logger != null)
                _logger.InfoFormat("RmqMessagePublisher: Regenerating message {0} with DeliveryTag of {1} to {2} with DeliveryTag of {3}", message.Id, DeliveryTag, messageId, 1);

            var headers = new Dictionary<string, object>
            {
                {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                {HeaderNames.TOPIC, message.Header.Topic},
                {HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString(CultureInfo.InvariantCulture)},
            };

            if (message.Header.CorrelationId != Guid.Empty)
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.ToString());

            message.Header.Bag.Each((header) =>
            {
                if (!HeadersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
            });

            headers.Add(HeaderNames.DELIVERY_TAG, DeliveryTag);

            if (delayMilliseconds > 0)
                headers.Add(HeaderNames.DELAY_MILLISECONDS, delayMilliseconds);

            if (!message.Header.Bag.Any(h => h.Key.Equals(HeaderNames.ORIGINAL_MESSAGE_ID, StringComparison.CurrentCultureIgnoreCase)))
                headers.Add(HeaderNames.ORIGINAL_MESSAGE_ID, message.Id.ToString());

            // To send it to the right queue use the default (empty) exchange
            _channel.BasicPublish(
                String.Empty,
                queueName,
                false,
                CreateBasicProperties(messageId, message.Header.TimeStamp, message.Body.BodyType, message.Header.ContentType, headers),
                message.Header.ContentType == "text/plain"
                    ? Encoding.UTF8.GetBytes(message.Body.Value)
                    : message.Body.Bytes);
        }

        /// <summary>
        /// Helper method to create the basic properties object used by RMQ to convey common additional characteristics
        /// about the message
        /// </summary>
        /// <param name="id">Unique message identifier</param>
        /// <param name="timeStamp">Timestamp associated with the message</param>
        /// <param name="type">Serialization type information to facilitate interoperability with consumers that may not be using a Data Type Channel pattern and thus need additional information to interpret the message body</param>
        /// <param name="contentType">Indicates how the content was serialized.  Examples are text/plan (the default) and application/x-protobuf for Google Protocol Buffers</param>
        /// <param name="headers">Optional headers</param>
        /// <returns></returns>
        private IBasicProperties CreateBasicProperties(Guid id, DateTime timeStamp, string type, string contentType, IDictionary<string, object> headers = null)
        {
            var basicProperties = _channel.CreateBasicProperties();

            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = false == string.IsNullOrEmpty(contentType) ? contentType : "text/plain";

            if (false == string.IsNullOrEmpty(type))
            {
                basicProperties.Type = type;
            }
            basicProperties.MessageId = id.ToString();
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(timeStamp));

            if (headers != null && headers.Any())
                basicProperties.Headers = headers;

            return basicProperties;
        }
    }
}