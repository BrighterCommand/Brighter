#region Licence
/* The MIT License (MIT)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class When_applying_classic_group_protocol_should_map_settings
{
    [Fact]
    public void Should_set_classic_group_protocol()
    {
        // Arrange
        var config = new ConsumerConfig();

        // Act
        new ClassicGroupProtocol().Apply(config);

        // Assert
        Assert.Equal(GroupProtocol.Classic, config.GroupProtocol.GetValueOrDefault());
    }

    [Fact]
    public void Should_map_configured_settings()
    {
        // Arrange
        var config = new ConsumerConfig();
        var groupProtocol = new ClassicGroupProtocol
        {
            SessionTimeout = TimeSpan.FromSeconds(30),
            HeartbeatInterval = TimeSpan.FromSeconds(3),
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        // Act
        groupProtocol.Apply(config);

        // Assert
        Assert.Equal(30000, config.SessionTimeoutMs.GetValueOrDefault());
        Assert.Equal(3000, config.HeartbeatIntervalMs.GetValueOrDefault());
        Assert.Equal(PartitionAssignmentStrategy.CooperativeSticky, config.PartitionAssignmentStrategy.GetValueOrDefault());
    }

    [Fact]
    public void Should_leave_unset_settings_at_kafka_defaults()
    {
        // Arrange
        var config = new ConsumerConfig();

        // Act
        new ClassicGroupProtocol().Apply(config);

        // Assert — unset properties must not be written, so librdkafka defaults apply
        Assert.Null(config.SessionTimeoutMs);
        Assert.Null(config.HeartbeatIntervalMs);
        Assert.Null(config.PartitionAssignmentStrategy);
    }
}
