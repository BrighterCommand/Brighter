using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgreSqlMessageConsumerNativeDelayTests : IDisposable
{
    private readonly Message _message;
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAChannelFactory _channelFactory;
    private readonly PostgresSubscription<MyCommand> _subscription;
    private readonly RoutingKey _topic;

    public PostgreSqlMessageConsumerNativeDelayTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Consumer-NativeDelay-Tests-{Guid.NewGuid()}";
        _topic = new RoutingKey($"Consumer-NativeDelay-Tests-{Guid.NewGuid()}");

        _message = new Message(
            new MessageHeader(myCommand.Id, _topic, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
        );

        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        _subscription = new PostgresSubscription<MyCommand>(
            new SubscriptionName(channelName),
            new ChannelName(_topic), new RoutingKey(_topic),
            messagePumpType: MessagePumpType.Reactor);

        _producerRegistry = new PostgresProducerRegistryFactory(
            new PostgresMessagingGatewayConnection(testHelper.Configuration),
            [new PostgresPublication { Topic = new RoutingKey(_topic) }]
        ).Create();

        _channelFactory = new PostgresChannelFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration));
    }

    [Fact]
    public void When_postgres_consumer_requeues_with_delay_should_use_native_sql()
    {
        // Arrange - send and receive a message
        ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(_message);
        var channel = _channelFactory.CreateSyncChannel(_subscription);
        var message = channel.Receive(TimeSpan.FromMilliseconds(2000));

        // Act - requeue with a meaningful delay
        var requeueDelay = TimeSpan.FromSeconds(2);
        bool requeued = channel.Requeue(message, requeueDelay);

        // Assert - requeue succeeded
        Assert.True(requeued);

        // Assert - message is NOT visible immediately (native SQL sets visible_timeout in the future)
        var immediateReceive = channel.Receive(TimeSpan.FromMilliseconds(500));
        Assert.Equal(MessageType.MT_NONE, immediateReceive.Header.MessageType);

        // Assert - message becomes visible after the delay elapses
        Thread.Sleep(requeueDelay);
        var delayedReceive = channel.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.Equal(MessageType.MT_COMMAND, delayedReceive.Header.MessageType);
        Assert.Equal(message.Body.Value, delayedReceive.Body.Value);

        // Cleanup
        channel.Acknowledge(delayedReceive);
    }

    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
