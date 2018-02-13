using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.Redis.LibLog;
using ServiceStack;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageCreator
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisMessageCreator>);
        
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
            
            using (var reader = new StringReader(redisMessage))
            {
                var header = reader.ReadLine();
                if (header.TrimEnd() != "<HEADER")
                {
                    _logger.Value.ErrorFormat("Expected message to begin with <HEADER, but was {0}", redisMessage);
                    return message;
                }
                
                var messageHeader = ReadHeader(reader.ReadLine());
                
                header = reader.ReadLine();
                if (header.TrimStart() != "HEADER/>")
                {
                    _logger.Value.ErrorFormat("Expected message to find end of HEADER/>, but was {0}", redisMessage);
                    return message;
                }

                var body = reader.ReadLine();
                if (body.TrimEnd() != "<BODY")
                {
                    _logger.Value.ErrorFormat("Expected message to have beginning of <BODY, but was {0}", redisMessage);
                    return message;
                }
                
                var messageBody = ReadBody(reader);

                body = reader.ReadLine();
                if (body.TrimStart() != "BODY/>")
                {
                    _logger.Value.ErrorFormat("Expected message to find end of BODY/>, but was {0}", redisMessage);
                    return message;
                }
                
                message = new Message(messageHeader, messageBody);

            }

            return message;
        }

        private MessageBody ReadBody(StringReader reader)
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
        private MessageHeader ReadHeader(string headersJson)
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
            if (headers.ContainsKey(HeaderNames.CONTENT_TYPE))
            {
                return new HeaderResult<string>(headers[HeaderNames.CONTENT_TYPE], true);
            }
            return new HeaderResult<string>(String.Empty, false);
        }

        private HeaderResult<Guid> ReadCorrelationId(Dictionary<string, string> headers)
        {
            var messageId = Guid.Empty;
            
            if (headers.ContainsKey(HeaderNames.CORRELATION_ID))
            {
                if (Guid.TryParse(headers[HeaderNames.CORRELATION_ID], out messageId))
                {
                    return new HeaderResult<Guid>(messageId, true);
                }
            }
            
            return new HeaderResult<Guid>(messageId, false);
        }

         private HeaderResult<int> ReadDelayedMilliseconds(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(HeaderNames.DELAYED_MILLISECONDS))
            {
                if (int.TryParse(headers[HeaderNames.DELAYED_MILLISECONDS], out int delayedMilliseconds))
                {
                    return new HeaderResult<int>(delayedMilliseconds, true); 
                }
            }
            return new HeaderResult<int>(0, true);
         }
        
        private HeaderResult<int> ReadHandledCount(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(HeaderNames.HANDLED_COUNT))
            {
                if (int.TryParse(headers[HeaderNames.HANDLED_COUNT], out int handledCount))
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

            if (headers.ContainsKey(HeaderNames.BAG))
            {
                var bagJson = headers[HeaderNames.BAG];
                var bag = JsonConvert.DeserializeObject<Dictionary<string, object>>(bagJson);
                return new HeaderResult<Dictionary<string, object>>(bag, true);
            }
            return new HeaderResult<Dictionary<string, object>>(new Dictionary<string, object>(), false);

        }

         private HeaderResult<MessageType> ReadMessageType(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(HeaderNames.MESSAGE_TYPE))
            {
                if (Enum.TryParse(headers[HeaderNames.MESSAGE_TYPE], out MessageType messageType))
                {
                    return new HeaderResult<MessageType>(messageType, true);
                }
            }
            
            return new HeaderResult<MessageType>(MessageType.MT_EVENT, true);
        }

        private HeaderResult<Guid> ReadMessageId(IDictionary<string, string> headers)
        {
            var messageId = Guid.Empty;
            
            if (headers.ContainsKey(HeaderNames.MESSAGE_ID))
            {
                if (Guid.TryParse(headers[HeaderNames.MESSAGE_ID], out messageId))
                {
                    return new HeaderResult<Guid>(messageId, true);
                }
            }
            
            return new HeaderResult<Guid>(messageId, false);
        }
        
        private HeaderResult<string> ReadReplyTo(Dictionary<string, string> headers)
        {
            if (headers.ContainsKey(HeaderNames.REPLY_TO))
            {
                return new HeaderResult<string>(headers[HeaderNames.REPLY_TO], true);
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
            if (headers.ContainsKey(HeaderNames.TIMESTAMP))
            {
                if(DateTime.TryParse(headers[HeaderNames.TIMESTAMP], out DateTime timestamp))
                {
                    return new HeaderResult<DateTime>(timestamp, true);
                }
            }
            return new HeaderResult<DateTime>(DateTime.UtcNow, true);
        }

        private HeaderResult<string> ReadTopic(Dictionary<string, string> headers)
        {
            var topic = string.Empty;
            if (headers.ContainsKey(HeaderNames.TOPIC))
            {
                return new HeaderResult<string>(headers[HeaderNames.TOPIC], false);
            }
            return new HeaderResult<string>(String.Empty, false);
        }

     }
}