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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    //These enums map to Confluent's ProducerConfig values, we just don't want a dependency on that in our interface

    /// <summary>
    /// What is the algorithm for mapping keys to partitions
    /// </summary>
    public enum Partitioner
    {
        Random,
        Consistent,
        ConsistentRandom,
        Murmur2,
        Murmur2Random,
    }
    
    /// <summary>
    /// When publishing to the broker, how many nodes must we replicate to before responding
    /// </summary>
    public enum Acks
    {
        All = -1, // 0xFFFFFFFF
        None = 0,
        Leader = 1,
    }
    
    public class KafkaPublication: Publication
    {
        /// <summary>
        /// The acks parameter controls how many ISR nodes must receive the 
        /// record before the producer can consider the write successful.
        /// </summary>
        public Acks Replication { get; set; } = Acks.All;

        /// <summary>
        /// Maximum number of messages batched in one MessageSet. 
        /// </summary>
        public int BatchNumberMessages { get; set; } = 10000;

        /// <summary>
        /// Messages are produced once only
        /// Will adjust the following if not set:
        /// `max.in.flight.requests.per.connection=5` (must be less than or equal to 5), `retries=INT32_MAX` (must be greater than 0), `acks=all`, `queuing.strategy=fifo`. 
        /// </summary>
        public bool EnableIdempotence { get; set; } = true;
        
         /// <summary>
         /// Maximum time, in milliseconds, for buffering data on the producer queue.
         /// </summary>
         public int LingerMs { get; set; } = 5;

         /// <summary>
        /// How many times to retry sending a failing MessageSet.
        /// Note: retrying may cause reordering, set the  max in flight to 1 if using this 
        /// </summary>
        public int MessageSendMaxRetries { get; set; } = 3;

        /// <summary>
        /// Local message timeout. "
        /// This value is only enforced locally and limits the time a produced message
        /// waits for successful delivery. A time of 0 is infinite.
        /// </summary>
        public int MessageTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Maximum number of in-flight requests the  client will send.
        /// We default this to 1, so as to allow retries to not de-order the stream
        /// </summary>
        public int MaxInFlightRequestsPerConnection { get; set; } = 1;

        /// <summary>
        /// How many partitions on this topic?
        /// </summary>
        public int NumPartitions { get; set; } = 1;
        
        /// <summary>
        /// How do we partition - defaults to consistent random
        /// </summary>
        public Partitioner Partitioner { get; set; } = Partitioner.ConsistentRandom;
        
        /// <summary>
        /// Maximum number of messages allowed on the producer queue.
        /// </summary>
        public int QueueBufferingMaxMessages { get; set; } = 100000;

        /// <summary>
        /// Maximum total message size sum allowed on the producer queue.
        /// </summary>
        public int QueueBufferingMaxKbytes { get; set; } = 1048576;

        /// <summary>
        /// What is the replication factor? How many nodes is the topic copied to on the broker?
        /// </summary>
        public short ReplicationFactor { get; set; } = 1;

        /// <summary>
        /// The backoff time before retrying a message send.
        /// </summary>
        public int RetryBackoff { get; set; } = 100;

        /// <summary>
        /// The ack timeout of the producer request. This value is only enforced by the broker
        /// \and relies on Replication being != AcksEnum.None.",
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 500;
        
        /// <summary>
        /// How long to wait when asking for topic metadata
        /// </summary>
        public int TopicFindTimeoutMs { get; set; } = 5000;
        
        /// <summary>
        /// The unique identifier for this producer, used with transactions
        /// </summary>
        /// <returns></returns>
        public string? TransactionalId { get; set; }

        /// <summary>
        /// The Kafka message header builder that builds the message header before sending to Kafka topic.
        /// Populate this property to override the default implementation of header builder.
        /// </summary>
        public IKafkaMessageHeaderBuilder MessageHeaderBuilder { get; set; } = KafkaDefaultMessageHeaderBuilder.Instance;
    }

    /// <summary>
    /// Represents a publication for Apache Kafka, associating a specific message type with the publication.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the request (message) that this publication handles.
    /// This type must be a class and implement the <see cref="IRequest"/> interface.
    /// </typeparam>
    public class KafkaPublication<T> : KafkaPublication
        where T: class, IRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaPublication{T}"/> class.
        /// </summary>
        public KafkaPublication()
        {
            RequestType = typeof(T);
        }
    }
}
