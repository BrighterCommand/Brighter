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
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

/// <summary>
/// When the MsSql consumer requeues a message with zero or null delay, it should use the
/// direct SQL queue send rather than creating a producer. This preserves existing behavior
/// and ensures immediate requeue without scheduler overhead.
/// </summary>
[Trait("Category", "MSSQL")]
public class When_mssql_consumer_requeues_with_zero_delay_should_use_direct_queue : IDisposable
{
    private readonly MsSqlMessageConsumer _consumer;
    private readonly MsSqlMessageProducer _producer;
    private readonly Message _message;
    private readonly string _topicName;

    public When_mssql_consumer_requeues_with_zero_delay_should_use_direct_queue()
    {
        // Arrange
        var testHelper = new MsSqlTestHelper();
        testHelper.SetupQueueDb();

        _topicName = $"Requeue-ZeroDelay-Tests-{Guid.NewGuid()}";
        var topic = new RoutingKey(_topicName);

        var myCommand = new MyCommand { Value = "Test" };

        _message = new Message(
            new MessageHeader(myCommand.Id, topic, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options)));

        _producer = new MsSqlMessageProducer(testHelper.QueueConfiguration);
        _consumer = new MsSqlMessageConsumer(testHelper.QueueConfiguration, _topicName);
    }

    [Fact]
    public void When_requeuing_with_zero_delay_should_send_directly_to_queue()
    {
        // Arrange - send and receive a message so it's in the queue
        _producer.Send(_message);
        var received = _consumer.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.NotEmpty(received);
        Assert.Equal(MessageType.MT_COMMAND, received[0].Header.MessageType);

        // Act - requeue with zero delay (should use direct queue send)
        var result = _consumer.Requeue(received[0], TimeSpan.Zero);

        // Assert - returns true
        Assert.True(result);

        // Assert - message is immediately available in queue (direct send, not scheduled)
        var requeued = _consumer.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.NotEmpty(requeued);
        Assert.Equal(_message.Body.Value, requeued[0].Body.Value);
    }

    [Fact]
    public void When_requeuing_with_null_delay_should_send_directly_to_queue()
    {
        // Arrange - send and receive a message
        _producer.Send(_message);
        var received = _consumer.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.NotEmpty(received);
        Assert.Equal(MessageType.MT_COMMAND, received[0].Header.MessageType);

        // Act - requeue with null delay (should default to zero and use direct queue)
        var result = _consumer.Requeue(received[0]);

        // Assert - returns true
        Assert.True(result);

        // Assert - message is immediately available
        var requeued = _consumer.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.NotEmpty(requeued);
        Assert.Equal(_message.Body.Value, requeued[0].Body.Value);
    }

    public void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
    }
}
