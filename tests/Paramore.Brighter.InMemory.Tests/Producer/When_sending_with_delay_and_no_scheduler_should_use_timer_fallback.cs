#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Producer;

/// <summary>
/// Tests that InMemoryMessageProducer.SendWithDelay throws ConfigurationException
/// when no scheduler is configured and delay is greater than zero.
/// </summary>
public class SchedulerNotConfiguredTests
{
    private readonly InMemoryMessageProducer _producer;
    private readonly Message _message;
    private readonly TimeSpan _delay;

    public SchedulerNotConfiguredTests()
    {
        // Arrange - no scheduler configured
        var bus = new InternalBus();
        _producer = new InMemoryMessageProducer(bus);
        // Note: Scheduler is NOT set - testing exception behavior

        var routingKey = new RoutingKey("test.topic");
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_EVENT),
            new MessageBody("test content"));
        _delay = TimeSpan.FromSeconds(30);
    }

    [Fact]
    public void When_sending_with_delay_and_no_scheduler_should_throw_exception()
    {
        // Assert
        Assert.Throws<ConfigurationException>(() =>
        {
            // Act
            _producer.SendWithDelay(_message, _delay);
        });
    }

    [Fact]
    public async Task When_sending_async_with_delay_and_no_scheduler_should_throw_exception()
    {
        // Assert
        await Assert.ThrowsAsync<ConfigurationException>(async () =>
        {
            // Act
            await _producer.SendWithDelayAsync(_message, _delay);
        });
    }
}
