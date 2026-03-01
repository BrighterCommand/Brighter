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

using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class When_mssql_consumer_factory_creates_consumer_should_pass_scheduler
{
    private readonly RelationalDatabaseConfiguration _configuration = new(
        "Server=localhost;Database=test;Trusted_Connection=True;",
        queueStoreTable: "BrighterQueue"
    );

    private readonly Subscription _subscription = new(
        subscriptionName: new SubscriptionName("test"),
        channelName: new ChannelName("test.queue"),
        routingKey: new RoutingKey("test.key"),
        requestType: typeof(Command),
        messagePumpType: MessagePumpType.Reactor
    );

    [Fact]
    public void Should_create_sync_consumer_when_scheduler_provided()
    {
        // Arrange — factory constructed with a scheduler
        var scheduler = new StubMessageScheduler();
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.Create(_subscription);

        // Assert — consumer is created successfully
        Assert.NotNull(consumer);
        Assert.IsType<MsSqlMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_async_consumer_when_scheduler_provided()
    {
        // Arrange — factory constructed with a scheduler
        var scheduler = new StubMessageScheduler();
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);

        // Act
        var consumer = factory.CreateAsync(_subscription);

        // Assert — consumer is created successfully
        Assert.NotNull(consumer);
        Assert.IsType<MsSqlMessageConsumer>(consumer);
    }

    [Fact]
    public void Should_create_consumer_without_scheduler_for_backward_compat()
    {
        // Arrange — factory constructed without a scheduler (backward compat)
        var factory = new MsSqlMessageConsumerFactory(_configuration);

        // Act
        var consumer = factory.Create(_subscription);

        // Assert — consumer is created successfully without scheduler
        Assert.NotNull(consumer);
        Assert.IsType<MsSqlMessageConsumer>(consumer);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
