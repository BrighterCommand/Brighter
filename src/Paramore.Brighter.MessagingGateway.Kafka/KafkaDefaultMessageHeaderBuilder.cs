#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Globalization;
using System.Linq;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <summary>
    /// This class serializes Brighter headers to Kafka. Kafka uses a byte[] for its header values. We convert all
    /// header values into a string, and then get a UTF8 encoded set of bytes for that string.
    /// </summary>
    public class KafkaDefaultMessageHeaderBuilder : IKafkaMessageHeaderBuilder
    {
        public static KafkaDefaultMessageHeaderBuilder Instance => new KafkaDefaultMessageHeaderBuilder();

        public Headers Build(Message message)
        {
            var headers = new Headers
            {
                // Cloud event
                new Header(HeaderNames.CLOUD_EVENTS_ID, message.Header.MessageId.Value.ToByteArray()),
                new Header(HeaderNames.CLOUD_EVENTS_SPEC_VERSION, message.Header.SpecVersion.ToByteArray()),
                new Header(HeaderNames.CLOUD_EVENTS_TYPE, message.Header.Type.ToByteArray()),
                new Header(HeaderNames.CLOUD_EVENTS_SOURCE, message.Header.Source.ToString().ToByteArray()),
                new Header(HeaderNames.CLOUD_EVENTS_TIME, message.Header.TimeStamp.ToRcf3339().ToByteArray()),
                
                // Brighter custom headers
                new Header(HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString().ToByteArray()),
                new Header(HeaderNames.TOPIC, message.Header.Topic.Value.ToByteArray()),
                
                // Backward compatibility with old brighter version
                new Header(HeaderNames.MESSAGE_ID, message.Header.MessageId.Value.ToByteArray()),
            };
            
            if (!string.IsNullOrEmpty(message.Header.Subject))
            {
                headers.Add(HeaderNames.CLOUD_EVENTS_SUBJECT, message.Header.Subject.ToByteArray());
            }
            
            if (message.Header.DataSchema != null)
            {
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, message.Header.DataSchema.ToString().ToByteArray());
            }
            
            if (!string.IsNullOrEmpty(message.Header.TraceParent))
            {
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, message.Header.TraceParent.Value.ToByteArray());
            }
            
            if (!string.IsNullOrEmpty(message.Header.TraceState))
            {
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_STATE, message.Header.TraceState.Value.ToByteArray());
            }
            
            if (message.Header.Baggage.Any())
            {
                headers.Add(HeaderNames.W3C_BAGGAGE, message.Header.Baggage.ToString().ToByteArray());
            }
                            
            if (!string.IsNullOrEmpty(message.Header.ContentType))
            {
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_CONTENT_TYPE, message.Header.ContentType.Value.ToByteArray());
            }
           
            
            var timeStampAsString = DateTimeOffset.UtcNow.DateTime.ToString(CultureInfo.InvariantCulture);
            if (message.Header.TimeStamp.DateTime != default)
            {
                timeStampAsString = message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture);
            }

            headers.Add(HeaderNames.TIMESTAMP, timeStampAsString.ToByteArray());
            
            if (!string.IsNullOrEmpty(message.Header.ContentType))
            {
                headers.Add(HeaderNames.CONTENT_TYPE, message.Header.ContentType.Value.ToByteArray());
            }
            
            if (message.Header.CorrelationId != string.Empty)
            {
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.Value.ToByteArray());
            }

            if (!string.IsNullOrEmpty(message.Header.PartitionKey))
            {
                headers.Add(HeaderNames.PARTITIONKEY, message.Header.PartitionKey.Value.ToByteArray());
            }

            if (!string.IsNullOrEmpty(message.Header.ReplyTo))
            {
                headers.Add(HeaderNames.REPLY_TO, message.Header.ReplyTo.ToByteArray());
            }

            headers.Add(HeaderNames.DELAYED_MILLISECONDS, message.Header.Delayed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture).ToByteArray());
            headers.Add(HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString().ToByteArray());


            message.Header.Bag
                .Where(x => !BrighterDefinedHeaders.HeadersToReset.Contains(x.Key))
                .Each(header =>
                {
                    switch (header.Value)
                    {
                        case string stringValue:
                            headers.Add(header.Key, stringValue.ToByteArray());
                            break;
                        case DateTimeOffset dateTimeOffsetValue:
                            headers.Add(header.Key, dateTimeOffsetValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                            break;
                        case DateTime dateTimeValue:
                            headers.Add(header.Key, dateTimeValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                            break;
                        case Guid guidValue:
                            headers.Add(header.Key, guidValue.ToString().ToByteArray());
                            break;
                        case bool boolValue:
                            headers.Add(header.Key, boolValue.ToString().ToByteArray());
                            break;
                        case int intValue:
                            headers.Add(header.Key, intValue.ToString().ToByteArray());
                            break;
                        case double doubleValue:
                            headers.Add(header.Key, doubleValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                            break;
                        case float floatValue:
                            headers.Add(header.Key, floatValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                            break;
                        case long longValue:
                            headers.Add(header.Key, longValue.ToString().ToByteArray());
                            break;
                        case byte[] byteArray:
                            headers.Add(header.Key, byteArray);
                            break;
                        default:
                            headers.Add(header.Key, header.Value.ToString().ToByteArray());
                            break;
                    }
                });

            return headers;
        }
    }
}
