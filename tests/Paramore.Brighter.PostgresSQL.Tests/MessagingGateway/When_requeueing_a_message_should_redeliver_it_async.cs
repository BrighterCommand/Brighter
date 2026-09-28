using System;
using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Trait("Category", "PostgresSql")]
public class PostgreSqlMessageConsumerRequeueAsyncTests : IDisposable
{
    private readonly Message _message;
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAChannelFactory _channelFactory;
    private readonly PostgresSubscription<MyCommand> _subscription;
    private readonly RoutingKey _topic;

    public PostgreSqlMessageConsumerRequeueAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Application.Json);
        var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid()}";
        _topic = new RoutingKey($"Consumer-Requeue-Tests-{Guid.NewGuid()}");

        _message = new Message(
            new MessageHeader(myCommand.Id, _topic, MessageType.MT_COMMAND, correlationId:correlationId,
                replyTo:new RoutingKey(replyTo), contentType:contentType),
            new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
        );

        var testHelper = new PostgresSqlTestHelper();
        testHelper.SetupDatabase();

        _subscription = new PostgresSubscription<MyCommand>(new SubscriptionName(channelName),
            new ChannelName(_topic), 
            new RoutingKey(_topic),
            messagePumpType: MessagePumpType.Proactor);
        
        _producerRegistry = new PostgresProducerRegistryFactory(
            new PostgresMessagingGatewayConnection(testHelper.Configuration),
            [new PostgresPublication {Topic = new RoutingKey(_topic)}]
        ).CreateAsync().Result;
        _channelFactory = new PostgresChannelFactory(new PostgresMessagingGatewayConnection(testHelper.Configuration));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1500)]
    public async Task When_requeueing_a_message_should_redeliver_it_async(int requeueDelayInMilliseconds)
    {
        // Arrange
        await _producerRegistry.LookupAsyncBy(_topic).SendAsync(_message);
        await using var channel = await _channelFactory.CreateAsyncChannelAsync(_subscription);
        var message = await channel.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(_message.Id, message.Id);

        // Act
        Assert.True(await channel.RequeueAsync(message, TimeSpan.FromMilliseconds(requeueDelayInMilliseconds)));

        // Assert
        var requeuedMessage = new Message();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
        {
            requeuedMessage = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
            if (requeuedMessage.Header.MessageType != MessageType.MT_NONE)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        Assert.Equal(MessageType.MT_COMMAND, requeuedMessage.Header.MessageType);
        await channel.AcknowledgeAsync(requeuedMessage);

        Assert.Equal(_message.Id, requeuedMessage.Id);
        Assert.Equal(_message.Body.Value, requeuedMessage.Body.Value);
    }
        
    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
