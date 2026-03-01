#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Reflection;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

public class PostgresMessageConsumerFactoryDlqTests : IDisposable
{
    private readonly PostgresConsumerFactory _factory;
    private IAmAMessageConsumerSync? _consumer;

    public PostgresMessageConsumerFactoryDlqTests()
    {
        //Arrange
        var configuration = new RelationalDatabaseConfiguration(
            "Host=localhost;Port=5432;Database=BrighterTests;Username=brighteruser;Password=Password1!");
        var connection = new PostgresMessagingGatewayConnection(configuration);
        _factory = new PostgresConsumerFactory(connection);
    }

    [Fact]
    public void When_creating_postgres_consumer_with_dlq_subscription_should_pass_routing_keys()
    {
        //Arrange
        var dlqRoutingKey = new RoutingKey("orders-dlq");
        var invalidRoutingKey = new RoutingKey("orders-invalid");

        var subscription = new PostgresSubscription<MyCommand>(
            subscriptionName: new SubscriptionName("test-subscription"),
            channelName: new ChannelName("test-channel"),
            routingKey: new RoutingKey("orders"),
            messagePumpType: MessagePumpType.Reactor,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey
        );

        //Act
        _consumer = _factory.Create(subscription);

        //Assert - verify the factory passed routing keys to the consumer
        Assert.NotNull(_consumer);

        var consumerType = _consumer.GetType();
        var dlqField = consumerType.GetField("_deadLetterRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var invalidField = consumerType.GetField("_invalidMessageRoutingKey",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(dlqField);
        Assert.NotNull(invalidField);

        var actualDlq = dlqField.GetValue(_consumer) as RoutingKey;
        var actualInvalid = invalidField.GetValue(_consumer) as RoutingKey;

        Assert.NotNull(actualDlq);
        Assert.Equal("orders-dlq", actualDlq.Value);

        Assert.NotNull(actualInvalid);
        Assert.Equal("orders-invalid", actualInvalid.Value);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
