using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Category("AWS")]
public class SqsMessageProducerResourcesAreTaggedAsyncTests : IAsyncDisposable
{
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _queueName;
    private readonly Message _message;

    public SqsMessageProducerResourcesAreTaggedAsyncTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        _queueName = $"Producer-Tag-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var channelName = new ChannelName(_queueName);

        var queueAttributes = new SqsAttributes(
            tags: new Dictionary<string, string> { { "Environment", "Test" } }
        );

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(_queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            queueAttributes: queueAttributes
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, new RoutingKey(_queueName), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(_awsConnection);
        _channelFactory.CreateAsyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication
            {
                ChannelName = channelName,
                MakeChannels = OnMissingChannel.Create
            });
    }

    [Test]
    public async Task When_posting_a_message_resources_are_tagged_async()
    {
        // arrange
        await _messageProducer.SendAsync(_message);

        // act - verify queue tags
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(_queueName);
        var queueTagsResponse = await sqsClient.ListQueueTagsAsync(
            new ListQueueTagsRequest { QueueUrl = queueUrlResponse.QueueUrl });

        // assert - queue has Environment=Test tag
        await Assert.That(queueTagsResponse.Tags.ContainsKey("Environment")).IsTrue();
        await Assert.That(queueTagsResponse.Tags["Environment"]).IsEqualTo("Test");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteQueueAsync();
        await _messageProducer.DisposeAsync();
    }
}
