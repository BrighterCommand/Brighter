﻿#region Licence
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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <inheritdoc />
    /// <summary>
    /// A factory for creating a Kafka message consumer from a <see cref="Subscription{T}"/>> 
    /// </summary>
    public class KafkaMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly KafkaMessagingGatewayConfiguration _configuration;

        /// <summary>
        /// Initializes a factory with the <see cref="KafkaMessagingGatewayConfiguration"/> used to connect to a Kafka Broker
        /// </summary>
        /// <param name="configuration">The <see cref="KafkaMessagingGatewayConfiguration"/> used to connect to the Broker</param>
        public KafkaMessageConsumerFactory(
            KafkaMessagingGatewayConfiguration configuration
            )
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Creates a consumer from the <see cref="Subscription{T}"/>
        /// </summary>
        /// <param name="subscription">The <see cref="KafkaSubscription"/> to read</param>
        /// <returns>A consumer that can be used to read from the stream</returns>
        public IAmAMessageConsumer Create(Subscription subscription)
        {
            KafkaSubscription kafkaSubscription = subscription as KafkaSubscription;  
            if (kafkaSubscription == null)
                throw new ConfigurationException("We expect an SQSConnection or SQSConnection<T> as a parameter");
            
            return new KafkaMessageConsumer(
                configuration: _configuration, 
                routingKey:kafkaSubscription.RoutingKey, //topic
                groupId: kafkaSubscription.GroupId, 
                offsetDefault: kafkaSubscription.OffsetDefault,
                sessionTimeoutMs: kafkaSubscription.SessionTimeoutMs,
                maxPollIntervalMs: kafkaSubscription.MaxPollIntervalMs,
                isolationLevel: kafkaSubscription.IsolationLevel,
                commitBatchSize: kafkaSubscription.CommitBatchSize,
                sweepUncommittedOffsetsIntervalMs: kafkaSubscription.SweepUncommittedOffsetsIntervalMs,
                readCommittedOffsetsTimeoutMs: kafkaSubscription.ReadCommittedOffsetsTimeOutMs,
                numPartitions: kafkaSubscription.NumPartitions,
                partitionAssignmentStrategy: kafkaSubscription.PartitionAssignmentStrategy,
                replicationFactor: kafkaSubscription.ReplicationFactor,
                topicFindTimeoutMs: kafkaSubscription.TopicFindTimeoutMs,
                makeChannels: kafkaSubscription.MakeChannels
                );
        }
    }
}
