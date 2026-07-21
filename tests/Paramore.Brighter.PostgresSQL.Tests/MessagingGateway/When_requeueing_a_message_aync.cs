using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Postgres;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway;

[Category("PostgresSql")]
public class PostgreSqlMessageConsumerRequeueTestsAsync : IDisposable
{
    private readonly Message _message;
    private IAmAProducerRegistry _producerRegistry;
    private readonly IAmAChannelFactory _channelFactory;
    private readonly PostgresSubscription<MyCommand> _subscription;
    private readonly RoutingKey _topic;
    private readonly PostgresSqlTestHelper _testHelper;

    public PostgreSqlMessageConsumerRequeueTestsAsync()
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

        _testHelper = new PostgresSqlTestHelper();
        _testHelper.SetupDatabase();

        _subscription = new PostgresSubscription<MyCommand>(new SubscriptionName(channelName),
            new ChannelName(_topic),
            new RoutingKey(_topic),
            messagePumpType: MessagePumpType.Proactor);
        _channelFactory = new PostgresChannelFactory(new PostgresMessagingGatewayConnection(_testHelper.Configuration));
    }

    [Before(Test)]
    public async Task Setup()
    {
        _producerRegistry = await new PostgresProducerRegistryFactory(
            new PostgresMessagingGatewayConnection(_testHelper.Configuration),
            [new PostgresPublication {Topic = new RoutingKey(_topic)}]
        ).CreateAsync();
    }

    [Test]
    public async Task When_requeueing_a_message_async()
    {
        await _producerRegistry.LookupAsyncBy(_topic).SendAsync(_message);
        var channel = await _channelFactory.CreateAsyncChannelAsync(_subscription);

        Message message = Message.Empty;
        for (var attempt = 0; attempt < 10 && message.IsEmpty; attempt++)
        {
            message = await channel.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        }

        await Assert.That(message.IsEmpty).IsFalse();
        await Assert.That(await channel.RequeueAsync(message, TimeSpan.FromMilliseconds(100))).IsTrue();

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Message requeuedMessage = Message.Empty;
        for (var attempt = 0; attempt < 10 && requeuedMessage.IsEmpty; attempt++)
        {
            requeuedMessage = await channel.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
        }

        //clear the queue
        await channel.AcknowledgeAsync(requeuedMessage);

        await Assert.That(requeuedMessage.Body.Value).IsEqualTo(message.Body.Value);
    }
        
    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
