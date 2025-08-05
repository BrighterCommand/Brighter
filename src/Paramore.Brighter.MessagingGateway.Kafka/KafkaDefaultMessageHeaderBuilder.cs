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
            var headers = new Headers();
            
            AddBrighterHeaders(headers, message);
            AddCloudEventHeaders(headers, message);
            AddUserDefinedBagHeaders(headers, message);

            return headers;
        }

        private static void AddBrighterHeaders(Headers headers, Message message)
        {
            headers.Add(new Header(HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString().ToByteArray()));
            headers.Add(new Header(HeaderNames.TOPIC, message.Header.Topic.Value.ToByteArray()));
            headers.Add(new Header(HeaderNames.MESSAGE_ID, message.Header.MessageId.Value.ToByteArray()));
            
            var timeStampAsString = message.Header.TimeStamp.DateTime != default
                ? message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture)
                : DateTimeOffset.UtcNow.DateTime.ToString(CultureInfo.InvariantCulture);
            
            headers.Add(HeaderNames.TIMESTAMP, timeStampAsString.ToByteArray());
            
            if (message.Header.ContentType is not null)
                headers.Add(HeaderNames.CONTENT_TYPE, message.Header.ContentType!.ToString().ToByteArray());
            
            if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.Value.ToByteArray());

            if (!PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
                headers.Add(HeaderNames.PARTITIONKEY, message.Header.PartitionKey.Value.ToByteArray());

            if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
                headers.Add(HeaderNames.REPLY_TO, message.Header.ReplyTo.Value.ToByteArray());

            headers.Add(HeaderNames.DELAYED_MILLISECONDS, message.Header.Delayed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture).ToByteArray());
            headers.Add(HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString().ToByteArray());
        }
        
        private void AddCloudEventHeaders(Headers headers, Message message)
        {
            headers.Add(new Header(HeaderNames.CLOUD_EVENTS_ID, message.Header.MessageId.Value.ToByteArray()));
            headers.Add(new Header(HeaderNames.CLOUD_EVENTS_SPEC_VERSION, message.Header.SpecVersion.ToByteArray()));
            headers.Add(new Header(HeaderNames.CLOUD_EVENTS_TYPE, message.Header.Type.Value.ToByteArray()));
            headers.Add(new Header(HeaderNames.CLOUD_EVENTS_SOURCE, message.Header.Source.ToString().ToByteArray()));
            headers.Add(new Header(HeaderNames.CLOUD_EVENTS_TIME, message.Header.TimeStamp.ToRfc3339().ToByteArray()));
            
            AddCLoudEventsOptionalHeaders(headers, message);
        }

        private void AddCLoudEventsOptionalHeaders(Headers headers, Message message)
        {
            if (!string.IsNullOrEmpty(message.Header.Subject))
                headers.Add(HeaderNames.CLOUD_EVENTS_SUBJECT, message.Header.Subject!.ToByteArray());
            
            if (message.Header.DataSchema != null)
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA, message.Header.DataSchema.ToString().ToByteArray());
            
            if (!TraceParent.IsNullOrEmpty(message.Header.TraceParent))
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_PARENT, message.Header.TraceParent!.Value.ToByteArray());
            
            if (!TraceState.IsNullOrEmpty(message.Header.TraceState))
                headers.Add(HeaderNames.CLOUD_EVENTS_TRACE_STATE, message.Header.TraceState!.Value.ToByteArray());
            
            if (message.Header.Baggage.Any())
                headers.Add(HeaderNames.W3C_BAGGAGE, message.Header.Baggage.ToString().ToByteArray());
                            
            if (message.Header.ContentType is not null)
                headers.Add(HeaderNames.CLOUD_EVENTS_DATA_CONTENT_TYPE, message.Header.ContentType.ToString().ToByteArray());
        }

        private void AddUserDefinedBagHeaders(Headers headers, Message message)
        {
            message.Header.Bag
                .Where(x => !BrighterDefinedHeaders.HeadersToReset.Contains(x.Key))
                .Each(header => AddUserDefinedBagHeader(headers, header.Key, header.Value));
        }

        private void AddUserDefinedBagHeader(Headers headers, string key, object value)
        {
            switch (value)
            {
                case string stringValue:
                    headers.Add(key, stringValue.ToByteArray());
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    headers.Add(key, dateTimeOffsetValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                    break;
                case DateTime dateTimeValue:
                    headers.Add(key, dateTimeValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                    break;
                case Guid guidValue:
                    headers.Add(key, guidValue.ToString().ToByteArray());
                    break;
                case bool boolValue:
                    headers.Add(key, boolValue.ToString().ToByteArray());
                    break;
                case int intValue:
                    headers.Add(key, intValue.ToString().ToByteArray());
                    break;
                case double doubleValue:
                    headers.Add(key, doubleValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                    break;
                case float floatValue:
                    headers.Add(key, floatValue.ToString(CultureInfo.InvariantCulture).ToByteArray());
                    break;
                case long longValue:
                    headers.Add(key, longValue.ToString().ToByteArray());
                    break;
                case byte[] byteArray:
                    headers.Add(key, byteArray);
                    break;
                default:
                    headers.Add(key, value.ToString()!.ToByteArray());
                    break;
            }
        }
    }
}
