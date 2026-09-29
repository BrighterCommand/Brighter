using System;
using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Property("Category", "PostgresSql")]
public class PostgreSqlMessageConsumerRequeueTests : IDisposable
{
    private readonly Message _message;
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAChannelFactory _channelFactory;
    private readonly PostgresSubscription<MyCommand> _subscription;
    private readonly RoutingKey _topic;

    public PostgreSqlMessageConsumerRequeueTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid()}";
        _topic = new RoutingKey($"Consumer-Requeue-Tests-{Guid.NewGuid()}");

        _message = new Message(
            new MessageHeader(myCommand.Id, _topic, MessageType.MT_COMMAND, correlationId:correlationId,
                replyTo:new RoutingKey(replyTo), contentType:contentType),
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
            [new PostgresPublication {Topic = new RoutingKey(_topic)}]
        ).Create();

        _channelFactory = new PostgresChannelFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration));
    }

    [Test]
    [Arguments(100)]
    [Arguments(1500)]
    public async System.Threading.Tasks.Task When_requeueing_a_message_should_redeliver_it(int requeueDelayInMilliseconds)
    {
        // Arrange
        ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(_message);
        using var channel = _channelFactory.CreateSyncChannel(_subscription);
        var message = channel.Receive(TimeSpan.FromMilliseconds(2000));
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Id).IsEqualTo(_message.Id);

        // Act
        await Assert.That(channel.Requeue(message, TimeSpan.FromMilliseconds(requeueDelayInMilliseconds))).IsTrue();

        // Assert
        var requeuedMessage = new Message();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            requeuedMessage = channel.Receive(TimeSpan.FromSeconds(1));
            if (requeuedMessage.Header.MessageType != MessageType.MT_NONE)
            {
                break;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(200));
        }

        await Assert.That(requeuedMessage.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        channel.Acknowledge(requeuedMessage);

        await Assert.That(requeuedMessage.Id).IsEqualTo(_message.Id);
        await Assert.That(requeuedMessage.Body.Value).IsEquivalentTo(_message.Body.Value);
    }

    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
