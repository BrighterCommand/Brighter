#region Licence
/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class HeaderNames
    {
        /// <summary>
        /// The message type
        /// </summary>
        public const string MESSAGE_TYPE = "MessageType";
        /// <summary>
        /// The message identifier
        /// </summary>
        public const string MESSAGE_ID = "MessageId";
        /// <summary>
        /// The correlation id
        /// </summary>
        public const string CORRELATION_ID = "CorrelationId";
        /// <summary>
        /// The time that message was sent
        /// </summary>
        public const string TIMESTAMP = "TimeStamp";
        /// <summary>
        /// The topic
        /// </summary>
        public const string TOPIC = "Topic";
        /// <summary>
        /// The key used to partition this message
        /// </summary>
        public static string PARTITIONKEY = "PartitionKey";
        /// <summary>
        /// What is the offset into the partition of the message
        /// </summary>
        public static string PARTITION_OFFSET = "TopicPartitionOffset";
    }
}
