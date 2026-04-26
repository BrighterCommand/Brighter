using System;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsMessageProducerDlqTests : IAsyncDisposable
{
    private readonly SqsMessageProducer _sender;
    private readonly IAmAChannelSync _channel;
    private readonly ChannelFactory _channelFactory;
    private readonly Message _message;
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly string _dlqChannelName;

    public SqsMessageProducerDlqTests()
    {
        MyCommand myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        _dlqChannelName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var correlationId = Guid.NewGuid().ToString();
        var queueName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);

        var queueAttributes = new SqsAttributes(
            redrivePolicy: new RedrivePolicy(new ChannelName(_dlqChannelName), 2),
            type: SqsType.Fifo,
            tags: new Dictionary<string, string> { { "Environment", "Test" } });

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            queueAttributes: queueAttributes,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: messageGroupId),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        //Must have credentials stored in the SDK Credentials store or shared credentials file
        _awsConnection = GatewayFactory.CreateFactory();

        _sender = new SqsMessageProducer(
            _awsConnection,
            new SqsPublication(channelName: channelName, queueAttributes: queueAttributes, makeChannels: OnMissingChannel.Create)
            );

        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);
    }

    [Test]
    public async Task When_requeueing_redrives_to_the_queue()
    {
        await _sender.SendAsync(_message);
        var receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        //should force us into the dlq
        receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        Task.Delay(5000);

        //inspect the dlq
        await Assert.That(await GetDLQCount(_dlqChannelName + ".fifo")).IsEqualTo(1);
    }

    private async Task<int> GetDLQCount(string queueName)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(queueName);
        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageAttributeNames = ["All", "ApproximateReceiveCount"]
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
        }

        return response.Messages.Count;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
