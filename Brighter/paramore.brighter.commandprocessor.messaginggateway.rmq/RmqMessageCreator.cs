
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
using Common.Logging;
using paramore.brighter.commandprocessor.extensions;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    internal class RmqMessageCreator
    {
        readonly ILog _logger;

        public RmqMessageCreator(ILog logger)
        {
            _logger = logger;
        }

        HeaderResult<string> ReadHeader(IDictionary<string, object> dict, string key, bool dieOnMissing = false)
        {
            if (false == dict.ContainsKey(key))
            {
                return new HeaderResult<string>(string.Empty, ! dieOnMissing);
            }

            var bytes = dict[key] as byte[];
            if (null == bytes)
            {
                _logger.Warn("The value of header" + key + " could not be cast to a byte array");
                return new HeaderResult<string>(null, false);
            }

            try
            {
                var val = Encoding.UTF8.GetString(bytes);
                return new HeaderResult<string>(val, true);
            }
            catch(Exception e)
            {
                var firstTwentyBytes = BitConverter.ToString(bytes.Take(20).ToArray());
                _logger.Warn("Failed to read the value of header " + key + " as UTF-8, first 20 byes follow: \n\t" + firstTwentyBytes, e);
                return new HeaderResult<string>(null, false);
            }
        }

        public Message CreateMessage(BasicDeliverEventArgs fromQueue)
        {
            var headers = fromQueue.BasicProperties.Headers ?? new Dictionary<string, object>();
            var topic = HeaderResult<string>.Empty();
            var messageId = HeaderResult<Guid>.Empty();
            var timeStamp = HeaderResult<DateTime>.Empty();
            
            Message message;
            try
            {
                topic = ReadTopic(fromQueue, headers);
                messageId = ReadMessageId(fromQueue.BasicProperties.MessageId);
                timeStamp = ReadTimeStamp(fromQueue.BasicProperties);
                var messageType = ReadMessageType(headers);

                if(false == (topic.Success && messageId.Success && messageType.Success))
                {
                    message = FailureMessage(topic, messageId);
                }
                else 
                {
                    string body = Encoding.UTF8.GetString(fromQueue.Body);

                    message = new Message(
                        timeStamp.Success ? new MessageHeader(messageId.Result, topic.Result, messageType.Result, timeStamp.Result) : new MessageHeader(messageId.Result, topic.Result, messageType.Result),
                        new MessageBody(body));

                    headers.Each(header => message.Header.Bag.Add(header.Key, Encoding.UTF8.GetString((byte[]) header.Value)));
                }
            }
            catch (Exception e)
            {
                _logger.Warn("Failed to create message from amqp message", e);
                message = FailureMessage(topic, messageId);
            }

            message.Header.Bag["DeliveryTag"] = fromQueue.DeliveryTag;
            return message;
        }


        private HeaderResult<DateTime> ReadTimeStamp(IBasicProperties basicProperties)
        {
            if (basicProperties.IsTimestampPresent())
            {
                return new HeaderResult<DateTime>(UnixTimestamp.DateTimeFromUnixTimestampSeconds(basicProperties.Timestamp.UnixTime), true);
            }

            return new HeaderResult<DateTime>(DateTime.UtcNow, false);
        }

        static Message FailureMessage(HeaderResult<string> topic, HeaderResult<Guid> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : Guid.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }

        HeaderResult<MessageType> ReadMessageType(IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.MESSAGE_TYPE)
                .Map(s => {
                              if (string.IsNullOrEmpty(s))
                              {
                                  return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
                              }
                              MessageType result;
                              var success = Enum.TryParse(s, true, out result);
                              return new HeaderResult<MessageType>(result, success);
                });
        }

        HeaderResult<string> ReadTopic(BasicDeliverEventArgs fromQueue, IDictionary<string, object> headers)
        {
            return ReadHeader(headers, HeaderNames.TOPIC).Map(s => {
                                                                       var val = string.IsNullOrEmpty(s) ? fromQueue.RoutingKey : s;
                                                                       return new HeaderResult<string>(val, true);
            });
                
        }

        private HeaderResult<Guid> ReadMessageId(string messageId)
        {
            Guid newMessageId = Guid.NewGuid();

            if (string.IsNullOrEmpty(messageId))
            {
                _logger.DebugFormat("No message id found in message MessageId, new message id is {0}", newMessageId);
                return new HeaderResult<Guid>(newMessageId, true);
            }

            if (Guid.TryParse(messageId, out newMessageId))
            {
                return new HeaderResult<Guid>(newMessageId, true);
            }

            _logger.DebugFormat("Could not parse message MessageId, new message id is {0}", Guid.Empty);
            return new HeaderResult<Guid>(Guid.Empty, false);

            
        }
    }
}