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
public class ClassicGroupProtocol : IGroupProtocol
{
    /// <summary>
    /// Gets or sets the consumer session timeout.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/> representing the session timeout, or <see langword="null"/> to use Kafka defaults.</value>
    public TimeSpan? SessionTimeoutMs {get; set;}

    /// <summary>
    /// Gets or sets the interval between consumer heartbeats.
    /// </summary>
    /// <value>A <see cref="TimeSpan"/> representing the heartbeat interval, or <see langword="null"/> to use Kafka defaults.</value>
    public TimeSpan? HeartbeatInterval { get; set; }

    /// <summary>
    /// Gets or sets the partition assignment strategy used by consumers in the group.
    /// </summary>
    /// <value>
    /// A <see cref="Confluent.Kafka.PartitionAssignmentStrategy"/> that controls how partitions are distributed across consumers.
    /// Defaults to <see cref="Confluent.Kafka.PartitionAssignmentStrategy.RoundRobin"/>.
    /// </value>
    public PartitionAssignmentStrategy PartitionAssignmentStrategy { get; set; } = PartitionAssignmentStrategy.RoundRobin;

    /// <inheritdoc />
    public void Apply(ConsumerConfig config)
    {
        config.GroupProtocol = GroupProtocol.Classic;
        config.PartitionAssignmentStrategy = PartitionAssignmentStrategy;
        if(SessionTimeoutMs.HasValue)
        {
            config.SessionTimeoutMs = Convert.ToInt32(SessionTimeoutMs.Value.TotalMilliseconds);
        }

        if (HeartbeatInterval.HasValue)
        {
            config.HeartbeatIntervalMs =  Convert.ToInt32(HeartbeatInterval.Value.TotalMilliseconds);
        }
    }
}
