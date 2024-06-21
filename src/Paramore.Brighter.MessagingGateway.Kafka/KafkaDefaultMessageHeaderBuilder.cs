﻿#region Licence
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
                new Header(HeaderNames.MESSAGE_TYPE, message.Header.MessageType.ToString().ToByteArray()),
                new Header(HeaderNames.TOPIC, message.Header.Topic.ToByteArray()),
                new Header(HeaderNames.MESSAGE_ID, message.Header.Id.ToByteArray()),
            };

            if (message.Header.TimeStamp != default)
                headers.Add(HeaderNames.TIMESTAMP, new DateTimeOffset(message.Header.TimeStamp).ToString().ToByteArray());
            else
                headers.Add(HeaderNames.TIMESTAMP, DateTimeOffset.UtcNow.ToString().ToByteArray());
            
            if (message.Header.CorrelationId != string.Empty)
                headers.Add(HeaderNames.CORRELATION_ID, message.Header.CorrelationId.ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.PartitionKey))
                headers.Add(HeaderNames.PARTITIONKEY, message.Header.PartitionKey.ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.ContentType))
                headers.Add(HeaderNames.CONTENT_TYPE, message.Header.ContentType.ToByteArray());

            if (!string.IsNullOrEmpty(message.Header.ReplyTo))
                headers.Add(HeaderNames.REPLY_TO, message.Header.ReplyTo.ToByteArray());
            
            headers.Add(HeaderNames.DELAYED_MILLISECONDS, message.Header.DelayedMilliseconds.ToString().ToByteArray());
            
            headers.Add(HeaderNames.HANDLED_COUNT, message.Header.HandledCount.ToString().ToByteArray());
            
            message.Header.Bag.Each((header) =>
            {
                if (!BrighterDefinedHeaders.HeadersToReset.Any(htr => htr.Equals(header.Key)))
                {
                    switch (header.Value)
                    {
                        case string stringValue:
                            headers.Add(header.Key, stringValue.ToByteArray());
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
                }
            });
            
            return headers;
        }
    }
}
