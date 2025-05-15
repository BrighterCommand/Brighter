using System;
using System.Text.Json;
using System.Threading;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgreSqlMessageConsumerRequeueTests
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
        const string contentType = "text\\plain";
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

    [Fact]
    public void When_requeueing_a_message()
    {
        ((IAmAMessageProducerSync)_producerRegistry.LookupBy(_topic)).Send(_message);
        var channel = _channelFactory.CreateSyncChannel(_subscription);
        var message = channel.Receive(TimeSpan.FromMilliseconds(2000));
        Assert.True(channel.Requeue(message, TimeSpan.FromMilliseconds(100)));

        Thread.Sleep(TimeSpan.FromMilliseconds(100));
        
        var requeuedMessage = channel.Receive(TimeSpan.FromMilliseconds(1000));

        //clear the queue
        channel.Acknowledge(requeuedMessage);

        Assert.Equal(message.Body.Value, requeuedMessage.Body.Value);
    }
}
