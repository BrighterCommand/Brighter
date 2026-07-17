// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka;

/// <summary>
/// Configures Kafka's classic consumer group protocol settings.
/// </summary>
/// <remarks>
/// This is the default <see cref="IGroupProtocol"/> used by <see cref="KafkaMessageConsumer"/> when no
/// <c>groupProtocol</c> is supplied. In that case the consumer back-fills any <see langword="null"/>
/// properties from its own constructor parameters before calling <see cref="Apply"/>:
/// <list type="bullet">
///   <item><description><see cref="SessionTimeout"/> is set from the consumer's <c>sessionTimeout</c> parameter (default 10 s).</description></item>
///   <item><description><see cref="PartitionAssignmentStrategy"/> is set from the consumer's <c>partitionAssignmentStrategy</c> parameter (default <see cref="Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin"/>).</description></item>
/// </list>
/// When you supply a pre-configured <see cref="ClassicGroupProtocol"/> instance explicitly, any properties
/// you have already set are preserved; only <see langword="null"/> ones are overridden.
/// </remarks>
public class ClassicGroupProtocol : IGroupProtocol
{
    /// <summary>
    /// Gets or sets the consumer session timeout.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the session timeout, or <see langword="null"/> to inherit the
    /// value from the <see cref="KafkaMessageConsumer"/> constructor (default 10 s).
    /// </value>
    public TimeSpan? SessionTimeout {get; set;}

    /// <summary>
    /// Gets or sets the interval between consumer heartbeats.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> representing the heartbeat interval, or <see langword="null"/> to use the
    /// Kafka broker default. Unlike <see cref="SessionTimeout"/>, this property has no consumer-level
    /// fallback and is only applied when explicitly set.
    /// </value>
    public TimeSpan? HeartbeatInterval { get; set; }

    /// <summary>
    /// Gets or sets the partition assignment strategy used by consumers in the group.
    /// </summary>
    /// <value>
    /// A <see cref="Confluent.Kafka.PartitionAssignmentStrategy"/> that controls how partitions are
    /// distributed across consumers, or <see langword="null"/> to inherit the value from the
    /// <see cref="KafkaMessageConsumer"/> constructor (default <see cref="Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin"/>).
    /// </value>
    public PartitionAssignmentStrategy? PartitionAssignmentStrategy { get; set; }

    /// <inheritdoc />
    public void Apply(ConsumerConfig config)
    {
        config.GroupProtocol = GroupProtocol.Classic;
        config.PartitionAssignmentStrategy = PartitionAssignmentStrategy;
        if(SessionTimeout.HasValue)
        {
            config.SessionTimeoutMs = Convert.ToInt32(SessionTimeout.Value.TotalMilliseconds);
        }

        if (HeartbeatInterval.HasValue)
        {
            config.HeartbeatIntervalMs =  Convert.ToInt32(HeartbeatInterval.Value.TotalMilliseconds);
        }
    }
}
