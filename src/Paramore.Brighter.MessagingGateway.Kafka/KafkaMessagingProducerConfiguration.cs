#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaMessagingProducerConfiguration
    {
        /// <summary>
        /// Maximum number of messages allowed on the 
        /// producer queue.
        /// </summary>
        public int? QueueBufferingMaxMessages { get; set; }

        /// <summary>
        /// Maximum total message size sum allowed on the 
        /// producer queue.
        /// </summary>
        public int? QueueBufferingMaxKbytes { get; set; }

        /// <summary>
        /// Maximum time, in milliseconds, for buffering data 
        ///on the producer queue.
        /// </summary>
        public TimeSpan? QueueBufferingMax { get; set; }

        /// <summary>
        /// The acks parameter controls how many 
        /// partition replicas must receive the 
        /// record before the producer can consider 
        /// the write successful.
        /// </summary>
        public Acks? Acks { get; set; }

        /// <summary>
        /// How many times to retry sending a failing MessageSet.
        /// Note: retrying may cause reordering.
        /// </summary>
        public int? MessageSendMaxRetries { get; set; }

        /// <summary>
        /// The backoff time before retrying 
        /// a message send.
        /// </summary>
        public TimeSpan? RetryBackoff { get; set; }

        /// <summary>
        /// Maximum number of messages batched in one MessageSet. 
        /// </summary>
        public int? BatchNumberMessages { get; set; }

        /// <summary>
        /// The ack timeout of the producer request.
	    /// This value is only enforced by the broker and relies 
	    /// on Acks being != AcksEnum.None.",
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Local message timeout. "
	    /// This value is only enforced locally and limits the time a 
	    /// produced message waits for successful delivery. 
        ///  A time of 0 is infinite.
        /// </summary>
        public TimeSpan? MessageTimeout { get; set; }

        public KafkaMessagingProducerConfiguration()
        {
        }

    }
}
