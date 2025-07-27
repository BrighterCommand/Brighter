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
using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
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
        /// {
        ///     "Id":"18669550-2069-48c5-923d-74a2e79c0748",
        ///     "TimeStamp":"2018-02-07T09:38:36Z",
        ///     "Topic":"test",
        ///     "MessageType":"1",
        ///     "HandledCount":"0",
        ///     "DelayedMilliseconds":"0",
        ///     "Bag":"{}",
        ///     "ReplyTo":"",
        ///     "ContentType":"text/plain",
        ///     "CorrelationId":"00000000-0000-0000-0000-000000000000",
        ///     "Source":"http://goparamore.io",
        ///     "Type":"goparamore.io.Paramore.Brighter.Message",
        ///     "DataSchema":"http://schema.example.com/test",
        ///     "Subject":"test-subject",
        ///     "PartitionKey":"partition1",
        ///     "TraceParent":"00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
        ///     "TraceState":"congo=t61rcWkgMzE",
        ///     "Baggage":"userId=alice"
        /// }
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
        /// We can't just de-serializee the headers from JSON 
        /// (1) We want to support Postel's Law and be tolerant to missing input where we can
        /// (2) JSON parsers can struggle with some types.
        /// </summary>
        /// <param name="headersJson">The raw header JSON</param>
        /// <returns></returns>
        private MessageHeader ReadHeader(string? headersJson)
        {
            if (headersJson is null)
                return MessageHeader.FailureMessageHeader(RoutingKey.Empty, Id.Empty);
            
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonSerialisationOptions.Options);  
            
            if (headers is null)
                return MessageHeader.FailureMessageHeader(RoutingKey.Empty, Id.Empty);
            
            var messageId = ReadMessageId(headers);
            var timeStamp = ReadTimeStamp(headers);
            var topic = ReadTopic(headers);
            var messageType = ReadMessageType(headers);
            var handledCount = ReadHandledCount(headers);
            var delayed = ReadDelay(headers);
            var bag = ReadMessageBag(headers);
            var replyTo = ReadReplyTo(headers);
            var contentType = ReadContentType(headers);
            var correlationId = ReadCorrelationId(headers);
            var source = ReadSource(headers);  
            var type = ReadType(headers);
            var dataSchema = ReadDataSchema(headers);
            var subject = ReadSubject(headers);
            var traceParent = ReadTraceParent(headers);
            var traceState = ReadTraceState(headers);
            var baggage = ReadBaggage(headers);
            

            var messageHeader = new MessageHeader(
                messageId: messageId.Result,
                topic: topic.Result,
                messageType: messageType.Result,
                source: source.Success ? source.Result : null,
                delayed: delayed.Success ? delayed.Result : TimeSpan.Zero,
                type: type.Success ? type.Result : null,
                timeStamp: timeStamp.Success ? timeStamp.Result : DateTime.UtcNow,
                correlationId: correlationId.Success ? correlationId.Result : Id.Empty,
                replyTo: replyTo.Success ? replyTo.Result : RoutingKey.Empty,
                contentType: contentType.Success ? contentType.Result : new ContentType(MediaTypeNames.Text.Plain),
                handledCount: handledCount.Result,
                dataSchema: dataSchema.Success ? dataSchema.Result : null,
                subject: subject.Success ? subject.Result : string.Empty,
                traceParent: traceParent.Result,
                traceState: traceState.Result,
                baggage: baggage.Result);

            if (!bag.Success) return messageHeader;

            var bagResult = bag.Result;
            foreach (var keyValue in bagResult)
                messageHeader.Bag.Add(keyValue.Key, keyValue.Value);

            return messageHeader;
        }

        private static HeaderResult<Baggage> ReadBaggage(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.W3C_BAGGAGE, out string? header))
            {
                var baggage = new Baggage();
                baggage.LoadBaggage(header);
                return new HeaderResult<Baggage>(baggage, true);
            }
            return new HeaderResult<Baggage>(new Baggage(), false);
        }
        
        private static HeaderResult<ContentType?> ReadContentType(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CONTENT_TYPE, out string? header))
            {
                var contentType = !string.IsNullOrEmpty(header) ? new ContentType(header) : new ContentType(MediaTypeNames.Text.Plain);
                return new HeaderResult<ContentType?>(contentType, true);
            }
            return new HeaderResult<ContentType?>(null, false);
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
        
        private HeaderResult<Uri?> ReadDataSchema(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, out string? header))
            {
                if (Uri.TryCreate(header, UriKind.Absolute, out Uri? dataSchema))
                {
                    return new HeaderResult<Uri?>(dataSchema, true);
                }
            }
            return new HeaderResult<Uri?>(null, false);
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

        private static HeaderResult<Id> ReadMessageId(IDictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.MESSAGE_ID, out string? header) && !string.IsNullOrEmpty(header))
            {
                return new HeaderResult<Id>(Id.Create(header), true);
            }
            
            return new HeaderResult<Id>(Id.Random(), true);
        }
        
        private static HeaderResult<RoutingKey> ReadReplyTo(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.REPLY_TO, out string? header))
            {
                return new HeaderResult<RoutingKey>(new RoutingKey(header), true);
            }
            return new HeaderResult<RoutingKey>(RoutingKey.Empty, false);
        }
        
        private HeaderResult<Uri?> ReadSource(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_SOURCE, out string? header))
            {
                return new HeaderResult<Uri?>(new Uri(header), true);
            }
            return new HeaderResult<Uri?>(null, false);
        }
        
        private HeaderResult<string> ReadSubject(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_SUBJECT, out string? header))
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
       
       private static HeaderResult<TraceParent> ReadTraceParent(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, out string? header))
            {
                return new HeaderResult<TraceParent>(new TraceParent(header), true);
            }
            return new HeaderResult<TraceParent>(TraceParent.Empty, false);
        }

        private static HeaderResult<TraceState> ReadTraceState(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TRACE_STATE, out string? header))
            {
                return new HeaderResult<TraceState>(new TraceState(header), true);
            }
            return new HeaderResult<TraceState>(TraceState.Empty, false);
        }
       
       private static HeaderResult<CloudEventsType> ReadType(Dictionary<string, string> headers)
        {
            if (headers.TryGetValue(HeaderNames.CLOUD_EVENTS_TYPE, out string? header))
            {
                return new HeaderResult<CloudEventsType>(new CloudEventsType(header), true);
            }
            return new HeaderResult<CloudEventsType>(CloudEventsType.Empty, false);
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
