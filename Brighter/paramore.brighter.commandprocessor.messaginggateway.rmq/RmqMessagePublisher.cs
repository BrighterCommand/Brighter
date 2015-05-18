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

using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RmqMessagePublisher.
    /// </summary>
    public class RmqMessagePublisher
    {
        private static string[] HeadersToReset = { HeaderNames.DELAY_MILLISECONDS, HeaderNames.MESSAGE_TYPE, HeaderNames.TOPIC, HeaderNames.HANDLED_COUNT };

        private readonly IModel _channel;
        private readonly string _exchangeName;

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
        }

        /// <summary>
        /// Publishes the message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="headers">User specified message headers.</param>
        public void PublishMessage(Message message, int delayMilliseconds)
        {
            _channel.BasicPublish(
                _exchangeName,
                message.Header.Topic,
                false,
                false,
                CreateBasicProperties(
                    message: message, 
                    additionalHeaders: delayMilliseconds > 0 ? new Dictionary<string, object> {{HeaderNames.DELAY_MILLISECONDS, delayMilliseconds}} : null),
                Encoding.UTF8.GetBytes(message.Body.Value));
        }

        private IBasicProperties CreateBasicProperties(Message message, IDictionary<string, object> additionalHeaders = null)
        {
            var basicProperties = _channel.CreateBasicProperties();
            basicProperties.DeliveryMode = 1;
            basicProperties.ContentType = "text/plain";
            basicProperties.MessageId = message.Id.ToString();
            basicProperties.Timestamp = new AmqpTimestamp(UnixTimestamp.GetUnixTimestampSeconds(message.Header.TimeStamp));
            basicProperties.Headers = new Dictionary<string, object>
                                      {
                                          {HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString()},
                                          {HeaderNames.TOPIC, message.Header.Topic},
                                          {HeaderNames.HANDLED_COUNT , message.Header.HandledCount.ToString(CultureInfo.InvariantCulture)}
                                      };

            if (additionalHeaders != null)
                additionalHeaders.Each((header) => basicProperties.Headers.Add(new KeyValuePair<string, object>(header.Key, header.Value)));
            
            message.Header.Bag.Each((header) => {
                if(!HeadersToReset.Any(htr => htr.Equals(header.Key))) basicProperties.Headers.Add(new KeyValuePair<string, object>(header.Key, header.Value));
            });

            return basicProperties;
        }
    }
}