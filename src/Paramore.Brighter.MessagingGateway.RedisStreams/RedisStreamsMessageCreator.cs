#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsMessageCreator
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisStreamsMessageCreator>);
        
        /// <summary>
        /// Create a Brighter Message from the Redis raw content
        /// Expected message shape is:
        /// {
        ///     {"TimeStamp":"2018-02-07T09:38:36Z"},
        ///     {"Id":"18669550-2069-48c5-923d-74a2e79c0748"},
        ///     {"Topic":"test"},
        ///     {"MessageType":"1"},
        ///     {"Bag":"{}"},
        ///     {"HandledCount":"0"},
        ///     {"DelayedMilliseconds":"0"},
        ///     {"CorrelationId":"00000000-0000-0000-0000-000000000000"},
        ///     {"ContentType":"text/plain"},
        ///     {"ReplyTo":"", Body="{JSON content}"}
        /// }
        /// </summary>
        /// <param name="redisMessage">The raw message read from the wire</param>
        /// <returns></returns>
        public Message CreateMessage(IEnumerable<NameValueEntry> streamEntryValues)
        {
            var message = new Message();
            if (!streamEntryValues.Any())
            {
                return message;
            }

            var valueMap = streamEntryValues.ToDictionary(t => t.Name.ToString(), t => t.Value);
            
            var messageHeader = ReadHeader(valueMap);
                
            var messageBody = ReadBody(valueMap);

            message = new Message(messageHeader, messageBody);

            return message;
        }

        private MessageBody ReadBody(Dictionary<string, RedisValue> reader)
        {
            var body
            return new MessageBody(reader.ReadLine());
        }

        /// <summary>
        /// We can't just de-serializee the headers from JSON using Newtonsoft
        /// (1) We want to support Postel's Law and be tolerant to missing input where we can
        /// (2) JSON parsers can struggle with some types.
        /// </summary>
        /// <param name="headersJson">The raw header JSON</param>
        /// <returns></returns>
        private MessageHeader ReadHeader(Dictionary<string, RedisValue> headersJson)
        {
            var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(headersJson);  
            //Read Message Id
            var messageId = ReadMessageId(headers);
            //Read TimeStamp
            var timeStamp = ReadTimeStamp(headers);
            //Read Topic
            var topic = ReadTopic(headers);
            //Read MessageType
            var messageType = ReadMessageType(headers);
           //Read HandledCount
            var handledCount = ReadHandledCount(headers);
            //Read DelayedMilliseconds
            var delayedMilliseconds = ReadDelayedMilliseconds(headers);
            //Read MessageBag
            var bag = ReadMessageBag(headers);
            //reply to
            var replyTo = ReadReplyTo(headers);
            //content type
            var contentType = ReadContentType(headers);
            //correlation id
            var correlationId = ReadCorrelationId(headers);
            

            if (!messageId.Success)
            {
                return FailureMessageHeader(topic, messageId);
            }
            else
            {
                var messageHeader = timeStamp.Success
                    ? new MessageHeader(messageId.Result, topic.Result, messageType.Result, timeStamp.Result, handledCount.Result, delayedMilliseconds.Result)
                    : new MessageHeader(messageId.Result, topic.Result, messageType.Result);

                if (replyTo.Success)
                {
                    messageHeader.ReplyTo = replyTo.Result;
                }

                if (contentType.Success)
                {
                    messageHeader.ContentType = contentType.Result;
                }

                if (bag.Success)
                {
                    foreach (var key in headers.Keys)
                    {
                        messageHeader.Bag.Add(key, headers[key]);
                    }
                }

                if (correlationId.Success)
                {
                    messageHeader.CorrelationId = correlationId.Result;
                }

                return messageHeader;
            }
        }

       /// <summary>
        /// We return an MT_UNACCEPTABLE message because we cannot process. Really this should go on to an
        /// Invalid Message Queue provided by the Control Bus
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="messageId"></param>
        /// <returns></returns>
        private MessageHeader FailureMessageHeader(HeaderResult<string> topic, HeaderResult<Guid> messageId)
        {
            return new MessageHeader(
                messageId.Success ? messageId.Result : Guid.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
        }
        
        private HeaderResult<string> ReadContentType(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.CONTENT_TYPE))
            {
                return new HeaderResult<string>(headers[MessageNames.CONTENT_TYPE], true);
            }
            return new HeaderResult<string>(String.Empty, false);
        }

        private HeaderResult<Guid> ReadCorrelationId(Dictionary<string, string> headers)
        {
            var messageId = Guid.Empty;
            
            if (headers.ContainsKey(MessageNames.CORRELATION_ID))
            {
                if (Guid.TryParse(headers[MessageNames.CORRELATION_ID], out messageId))
                {
                    return new HeaderResult<Guid>(messageId, true);
                }
            }
            
            return new HeaderResult<Guid>(messageId, false);
        }

         private HeaderResult<int> ReadDelayedMilliseconds(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.DELAYED_MILLISECONDS))
            {
                if (int.TryParse(headers[MessageNames.DELAYED_MILLISECONDS], out int delayedMilliseconds))
                {
                    return new HeaderResult<int>(delayedMilliseconds, true); 
                }
            }
            return new HeaderResult<int>(0, true);
         }
        
        private HeaderResult<int> ReadHandledCount(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.HANDLED_COUNT))
            {
                if (int.TryParse(headers[MessageNames.HANDLED_COUNT], out int handledCount))
                {
                    return new HeaderResult<int>(handledCount, true); 
                }
            }
            
            return new HeaderResult<int>(0, true);
        }
        
        /// <summary>
        /// The bag is JSON dictionary, so we just need to serialize that dictionary and set values
        /// The one thing to watch for here is that we don't know about types in a bag, and as such
        /// we can't do any conversion from a string. So the consumer of the bag will need to
        /// convert to their target type
        /// </summary>
        /// <param name="headers">The raw json</param>
        /// <returns>A dictionary, either empty if key missing or matching contents if present (could be mepty)</returns>
        private HeaderResult<Dictionary<string, object>> ReadMessageBag(Dictionary<string, string> headers)
        {

            if (headers.ContainsKey(MessageNames.BAG))
            {
                var bagJson = headers[MessageNames.BAG];
                var bag = JsonConvert.DeserializeObject<Dictionary<string, object>>(bagJson);
                return new HeaderResult<Dictionary<string, object>>(bag, true);
            }
            return new HeaderResult<Dictionary<string, object>>(new Dictionary<string, object>(), false);

        }

         private HeaderResult<MessageType> ReadMessageType(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.MESSAGE_TYPE))
            {
                if (Enum.TryParse(headers[MessageNames.MESSAGE_TYPE], out MessageType messageType))
                {
                    return new HeaderResult<MessageType>(messageType, true);
                }
            }
            
            return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
        }

        private HeaderResult<Guid> ReadMessageId(IDictionary<string, string> headers)
        {
            var messageId = Guid.Empty;
            
            if (headers.ContainsKey(MessageNames.MESSAGE_ID))
            {
                if (Guid.TryParse(headers[MessageNames.MESSAGE_ID], out messageId))
                {
                    return new HeaderResult<Guid>(messageId, true);
                }
            }
            
            return new HeaderResult<Guid>(messageId, false);
        }
        
        private HeaderResult<string> ReadReplyTo(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.REPLY_TO))
            {
                return new HeaderResult<string>(headers[MessageNames.REPLY_TO], true);
            }
            return new HeaderResult<string>(string.Empty, false);
        }

       /// <summary>
        /// Note that RMQ uses a unix timestamp, we just Newtonsoft's JSON date format in Redis 
        /// </summary>
        /// <param name="headers">The collection of headers</param>
        /// <returns>The result, always a success because we don't break for missing timestamp, just use now</returns>
        private HeaderResult<DateTime> ReadTimeStamp(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(MessageNames.TIMESTAMP))
            {
                if(DateTime.TryParse(headers[MessageNames.TIMESTAMP], out DateTime timestamp))
                {
                    return new HeaderResult<DateTime>(timestamp, true);
                }
            }
            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private HeaderResult<string> ReadTopic(Dictionary<string, string> headers)
        {
            var topic = string.Empty;
            if (headers.ContainsKey(MessageNames.TOPIC))
            {
                return new HeaderResult<string>(headers[MessageNames.TOPIC], false);
            }
            return new HeaderResult<string>(String.Empty, false);
        }

     }
}
