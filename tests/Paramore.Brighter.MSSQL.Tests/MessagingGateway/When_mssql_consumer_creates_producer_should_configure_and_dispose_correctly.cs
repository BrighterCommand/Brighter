#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

/// <summary>
/// Verifies that the MsSql consumer correctly wires the scheduler through to the
/// lazily-created requeue producer, and that disposal works in all cases.
/// Uses a non-zero delay to exercise the full scheduler path (lesson from Kafka/MQTT).
/// </summary>
[Trait("Category", "MSSQL")]
public class When_mssql_consumer_creates_producer_should_configure_and_dispose_correctly
{
    private readonly MsSqlTestHelper _testHelper;
    private readonly string _topicName;

    public When_mssql_consumer_creates_producer_should_configure_and_dispose_correctly()
    {
        _testHelper = new MsSqlTestHelper();
        _testHelper.SetupQueueDb();
        _topicName = $"Producer-Config-Tests-{Guid.NewGuid()}";
    }

    [Fact]
    public void When_requeuing_with_delay_should_wire_scheduler_to_producer()
    {
        // Arrange
        var scheduler = new SpySchedulerSync();
        var consumer = new MsSqlMessageConsumer(
            _testHelper.QueueConfiguration,
            _topicName,
            scheduler);

        var topic = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test scheduler wiring"));

        // Act - requeue with non-zero delay to exercise the scheduler path
        consumer.Requeue(message, TimeSpan.FromSeconds(5));

        // Assert - scheduler was called, proving it was wired through to the producer
        Assert.True(scheduler.ScheduleCalled);
        Assert.Equal(message.Body.Value, scheduler.ScheduledMessage?.Body.Value);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.ScheduledDelay);

        // Cleanup
        consumer.Dispose();
    }

    [Fact]
    public void When_disposing_after_requeue_should_not_throw()
    {
        // Arrange
        var scheduler = new SpySchedulerSync();
        var consumer = new MsSqlMessageConsumer(
            _testHelper.QueueConfiguration,
            _topicName,
            scheduler);

        var topic = new RoutingKey(_topicName);
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND),
            new MessageBody("test dispose after requeue"));

        consumer.Requeue(message, TimeSpan.FromSeconds(5));

        // Act & Assert - dispose after producer was created should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void When_disposing_without_requeue_should_not_throw()
    {
        // Arrange - create consumer but never requeue (producer never created)
        var scheduler = new SpySchedulerSync();
        var consumer = new MsSqlMessageConsumer(
            _testHelper.QueueConfiguration,
            _topicName,
            scheduler);

        // Act & Assert - dispose without producer creation should not throw
        var exception = Record.Exception(() => consumer.Dispose());
        Assert.Null(exception);
    }

    private sealed class SpySchedulerSync : IAmAMessageSchedulerSync
    {
        public bool ScheduleCalled { get; private set; }
        public Message? ScheduledMessage { get; private set; }
        public TimeSpan? ScheduledDelay { get; private set; }

        public string Schedule(Message message, DateTimeOffset at)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            return Guid.NewGuid().ToString();
        }

        public string Schedule(Message message, TimeSpan delay)
        {
            ScheduleCalled = true;
            ScheduledMessage = message;
            ScheduledDelay = delay;
            return Guid.NewGuid().ToString();
        }

        public bool ReScheduler(string schedulerId, DateTimeOffset at) => true;
        public bool ReScheduler(string schedulerId, TimeSpan delay) => true;
        public void Cancel(string id) { }
    }
}
