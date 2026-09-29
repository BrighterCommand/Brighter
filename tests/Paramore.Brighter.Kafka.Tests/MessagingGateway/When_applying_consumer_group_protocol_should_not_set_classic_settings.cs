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

using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;


namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class When_applying_consumer_group_protocol_should_not_set_classic_settings
{
    [Test]
    public async System.Threading.Tasks.Task Should_set_consumer_group_protocol()
    {
        // Arrange
        var config = new ConsumerConfig();

        // Act
        new ConsumerGroupProtocol().Apply(config);

        // Assert
        await Assert.That(config.GroupProtocol.GetValueOrDefault()).IsEqualTo(GroupProtocol.Consumer);
    }

    [Test]
    public async System.Threading.Tasks.Task Should_map_consumer_protocol_settings()
    {
        // Arrange
        var config = new ConsumerConfig();
        var groupProtocol = new ConsumerGroupProtocol
        {
            GroupRemoteAssignor = "range",
            GroupInstanceId = "consumer-1"
        };

        // Act
        groupProtocol.Apply(config);

        // Assert
        await Assert.That(config.GroupRemoteAssignor).IsEqualTo("range");
        await Assert.That(config.GroupInstanceId).IsEqualTo("consumer-1");
    }

    [Test]
    public async System.Threading.Tasks.Task Should_not_set_classic_only_settings()
    {
        // Arrange — librdkafka rejects a consumer creation when session.timeout.ms,
        // heartbeat.interval.ms or partition.assignment.strategy are present
        // alongside group.protocol=consumer
        var config = new ConsumerConfig();

        // Act
        new ConsumerGroupProtocol().Apply(config);

        // Assert
        await Assert.That(config.SessionTimeoutMs).IsNull();
        await Assert.That(config.HeartbeatIntervalMs).IsNull();
        await Assert.That(config.PartitionAssignmentStrategy).IsNull();
    }
}
