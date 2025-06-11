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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ServiceStack;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public partial class RedisMessageCreator
    {
        private static readonly ILogger s_logger= ApplicationLogging.CreateLogger<RedisMessageCreator>();
        
        /// <summary>
        /// Create a Brighter Message from the Redis raw content
        /// Expected message shape is:
        ///
        /// <HEADER 
        /// {"TimeStamp":"2018-02-07T09:38:36Z","Id":"18669550-2069-48c5-923d-74a2e79c0748","Topic":"test","MessageType":"1","Bag":"{}","HandledCount":"0","DelayedMilliseconds":"0","CorrelationId":"00000000-0000-0000-0000-000000000000","ContentType":"text/plain","ReplyTo":""}
        /// HEADER/>
        /// <BODY
        ///    more test content
        /// BODY/>
        ///
        /// </summary>
        /// <param name="redisMessage">The raw message read from the wire</param>
        /// <returns></returns>
        public Message CreateMessage(string redisMessage)
        {
            var message = new Message();
            if (redisMessage.IsNullOrEmpty())
            {
                return message;
            }

            using var reader = new StringReader(redisMessage);
            var header = reader.ReadLine();
            if (header is null || header.TrimEnd() != "<HEADER")
            {
                Log.ExpectedHeaderError(s_logger, redisMessage);
                return message;
            }
                
            var messageHeader = ReadHeader(reader.ReadLine());
                
            header = reader.ReadLine();
            if (header is null || header.TrimStart() != "HEADER/>")
            {
                Log.ExpectedHeaderEndError(s_logger, redisMessage);
                return message;
            }

            var body = reader.ReadLine();
            if (body is null)
            {
                Log.ExpectedBodyError(s_logger, redisMessage);
                return message;
            }
            
            if (body.TrimEnd() != "<BODY")
            {
                Log.ExpectedBodyStartError(s_logger, redisMessage);
                return message;
            }
                
            var messageBody = ReadBody(reader);

            body = reader.ReadLine();
            if (body is null)
            {
                Log.ExpectedBodyError(s_logger, redisMessage);
                return message;
            }
            
            if (body.TrimStart() != "BODY/>")
            {
                Log.ExpectedBodyEndError(s_logger, redisMessage);
                return message;
            }
                
            message = new Message(messageHeader, messageBody);

            return message;
        }

        private static MessageBody ReadBody(StringReader reader)
        {
            return new MessageBody(reader.ReadLine());
        }

        /// <summary>
        /// We can't just de-serializee the headers from JSON using Newtonsoft
        /// (1) We want to support Postel's Law and be tolerant to missing input where we can
        /// (2) JSON parsers can struggle with some types.
        /// </summary>
        /// <param name="headersJson">The raw header JSON</param>
        /// <returns></returns>
        private MessageHeader ReadHeader(string? headersJson)
        {
            if (headersJson is null)
            {
                return FailureMessageHeader(
                    new HeaderResult<RoutingKey>(RoutingKey.Empty, false), 
                    new HeaderResult<string>(string.Empty, false)
                );
            }
            
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonSerialisationOptions.Options);  
            
            if (headers is null)
                return FailureMessageHeader(
                    new HeaderResult<RoutingKey>(RoutingKey.Empty, false), 
                    new HeaderResult<string>(string.Empty, false)
                );
            
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
            var delayed = ReadDelay(headers);
            //Read MessageBag
            var bag = ReadMessageBag(headers);
            //reply to
            var replyTo = ReadReplyTo(headers);
            //content type
            var contentType = ReadContentType(headers);
            //correlation id
            var correlationId = ReadCorrelationId(headers);
            
            //TODO:CLOUD_EVENTS parse from headers

            if (!messageId.Success)
            {
                return FailureMessageHeader(topic, messageId);
            }
            else
            {
                var messageHeader = new MessageHeader(
                    messageId: messageId.Result, 
                    topic:topic.Result, 
                    messageType:messageType.Result,
                    source: null,
                    type: "",
                    timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                    correlationId: "",
                    replyTo: new RoutingKey(replyTo.Result),
                    contentType: "",
                    handledCount: handledCount.Result,
                    dataSchema: null,
                    subject: null,
                    delayed: delayed.Result);

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
                    foreach (var keyValue in headers)
                    {
                        messageHeader.Bag.Add(keyValue.Key, keyValue.Value);
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
        private static MessageHeader FailureMessageHeader(HeaderResult<RoutingKey> topic, HeaderResult<string> messageId)
        {
            return new MessageHeader(
                messageId.Success ? messageId.Result : string.Empty,
                topic.Success ? topic.Result : RoutingKey.Empty,
                MessageType.MT_UNACCEPTABLE);
        }
        
        private static HeaderResult<string> ReadContentType(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CONTENT_TYPE, out string? header))
            {
                return new HeaderResult<string>(header, true);
            }
            return new HeaderResult<string>(String.Empty, false);
        }

        private static HeaderResult<string> ReadCorrelationId(Dictionary<string, string> headers)
        {
            var newCorrelationId = string.Empty;
            
            if (headers.TryGetValue(HeaderNames.CORRELATION_ID, out string? correlatonId))
            {
                return new HeaderResult<string>(correlatonId, true);
            }
            
            return new HeaderResult<string>(newCorrelationId, false);
        }

        private static HeaderResult<TimeSpan> ReadDelay(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.DELAYED_MILLISECONDS, out string? header))
            {
                if (int.TryParse(header, out int delayedMilliseconds))
                {
                    return new HeaderResult<TimeSpan>(TimeSpan.FromMilliseconds(delayedMilliseconds), true); 
                }
            }
            return new HeaderResult<TimeSpan>(TimeSpan.Zero, true);
         }
        
        private static HeaderResult<int> ReadHandledCount(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.HANDLED_COUNT, out string? header))
            {
                if (int.TryParse(header, out int handledCount))
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
        private static HeaderResult<Dictionary<string, object>> ReadMessageBag(Dictionary<string, string> headers)
        {

            if (headers.TryGetValue(HeaderNames.BAG, out string? header))
            {
                var bag = JsonSerializer.Deserialize<Dictionary<string, object>>(header, JsonSerialisationOptions.Options);
                if (bag is null)
                    return new HeaderResult<Dictionary<string, object>>(new Dictionary<string, object>(), false);
                
                return new HeaderResult<Dictionary<string, object>>(bag, true);
            }
            return new HeaderResult<Dictionary<string, object>>(new Dictionary<string, object>(), false);

        }

         private static HeaderResult<MessageType> ReadMessageType(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.MESSAGE_TYPE, out string? header))
            {
                if (Enum.TryParse(header, out MessageType messageType))
                {
                    return new HeaderResult<MessageType>(messageType, true);
                }
            }
            
            return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
        }

        private static HeaderResult<string> ReadMessageId(IDictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.MESSAGE_ID, out string? header))
            {
                    return new HeaderResult<string>(header, true);
            }
            
            return new HeaderResult<string>(string.Empty, false);
        }
        
        private static HeaderResult<string> ReadReplyTo(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.REPLY_TO, out string? header))
            {
                return new HeaderResult<string>(header, true);
            }
            return new HeaderResult<string>(string.Empty, false);
        }

       /// <summary>
        /// Note that RMQ uses a unix timestamp, we just System.Text's JSON date format in Redis 
        /// </summary>
        /// <param name="headers">The collection of headers</param>
        /// <returns>The result, always a success because we don't break for missing timestamp, just use now</returns>
        private static HeaderResult<DateTime> ReadTimeStamp(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.TIMESTAMP, out string? header))
            {
                if(DateTime.TryParse(header, out DateTime timestamp))
                {
                    return new HeaderResult<DateTime>(timestamp, true);
                }
            }
            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private static HeaderResult<RoutingKey> ReadTopic(Dictionary<string, string> headers)
        {
            var topic = string.Empty;
            if (headers.TryGetValue(HeaderNames.TOPIC, out string? header))
            {
                return new HeaderResult<RoutingKey>(new RoutingKey(header), false);
            }
            return new HeaderResult<RoutingKey>(RoutingKey.Empty, false);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Error, "Expected message to begin with <HEADER, but was {ErrorMessage}")]
            public static partial void ExpectedHeaderError(ILogger logger, string errorMessage);

            [LoggerMessage(LogLevel.Error, "Expected message to find end of HEADER/>, but was {ErrorMessage}")]
            public static partial void ExpectedHeaderEndError(ILogger logger, string errorMessage);

            [LoggerMessage(LogLevel.Error, "Expected message to have a body, but was {ErrorMessage}")]
            public static partial void ExpectedBodyError(ILogger logger, string errorMessage);

            [LoggerMessage(LogLevel.Error, "Expected message to have beginning of <BODY, but was {ErrorMessage}")]
            public static partial void ExpectedBodyStartError(ILogger logger, string errorMessage);

            [LoggerMessage(LogLevel.Error, "Expected message to find end of BODY/>, but was {ErrorMessage}")]
            public static partial void ExpectedBodyEndError(ILogger logger, string errorMessage);
        }
     }
}
