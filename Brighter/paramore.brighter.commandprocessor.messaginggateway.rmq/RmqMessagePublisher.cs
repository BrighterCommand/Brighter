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
        private static string[] HeadersToReset = { HeaderNames.DELAY_MILLISECONDS, HeaderNames.MESSAGE_TYPE, HeaderNames.TOPIC, HeaderNames.HANDLED_COUNT, HeaderNames.DELIVERY_TAG };

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
        /// <param name="headers">User specified message headers.</param>
        /// <param name="regenerate">Generate new unique message identifier.</param>
        public void PublishMessage(Message message, int delayMilliseconds)
        {
            var messageId = message.Id;
            var deliveryTag = message.Header.Bag.ContainsKey(HeaderNames.DELIVERY_TAG) ? message.GetDeliveryTag().ToString() : null;

            var headers = new Dictionary<string, object>
                                      {
                                          {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                                          {HeaderNames.TOPIC, message.Header.Topic},
                                          {HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString(CultureInfo.InvariantCulture)},
                                      };

            message.Header.Bag.Each((header) =>
            {
                if (!HeadersToReset.Any(htr => htr.Equals(header.Key))) headers.Add(header.Key, header.Value);
            });

            if (!String.IsNullOrEmpty(deliveryTag))
                headers.Add(HeaderNames.DELIVERY_TAG, deliveryTag);

            if (delayMilliseconds > 0)
                headers.Add(HeaderNames.DELAY_MILLISECONDS, delayMilliseconds);

            _channel.BasicPublish(
                _exchangeName,
                message.Header.Topic,
                false,
                false,
                CreateBasicProperties(messageId, message.Header.TimeStamp, headers),
                Encoding.UTF8.GetBytes(message.Body.Value));
        }

        /// <summary>
        /// Requeues the message.
        /// </summary>
        /// <param name="message">The message.</param>
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
                false,
                CreateBasicProperties(messageId, message.Header.TimeStamp, headers),
                Encoding.UTF8.GetBytes(message.Body.Value));
        }

        private IBasicProperties CreateBasicProperties(Guid id, DateTime timeStamp, IDictionary<string, object> headers = null)
        {
            var basicProperties = _channel.CreateBasicProperties();

            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = id.ToString();
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(timeStamp));

            if (headers != null && headers.Any())
                basicProperties.Headers = headers;

            return basicProperties;
        }
    }
}