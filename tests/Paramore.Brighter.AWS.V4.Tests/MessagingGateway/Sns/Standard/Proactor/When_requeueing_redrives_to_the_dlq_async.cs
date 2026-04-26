using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Proactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsMessageProducerDlqTestsAsync : IAsyncDisposable
{
    private SnsMessageProducer _sender;
    private IAmAChannelAsync _channel;
    private ChannelFactory _channelFactory;
    private Message _message;
    private AWSMessagingGatewayConnection _awsConnection;
    private ChannelName _deadLetterChannel;

    [Before(Test)]
    public async Task Setup()
    {
        MyCommand myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);  
        var queueName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var dlQueue = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        _deadLetterChannel = new ChannelName(dlQueue);
        
        SqsSubscription<MyCommand> subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: new SqsAttributes(
                redrivePolicy: new RedrivePolicy(_deadLetterChannel!, 2),
                tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _awsConnection = GatewayFactory.CreateFactory();

        _sender = new SnsMessageProducer(_awsConnection,  new SnsPublication { Topic = routingKey, MakeChannels = OnMissingChannel.Create });

        await _sender.ConfirmTopicExistsAsync(topicName);

        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = await _channelFactory.CreateAsyncChannelAsync(subscription);
    }

    [Test]
    public async Task When_requeueing_redrives_to_the_queue_async()
    {
        await _sender.SendAsync(_message);
        var receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        await _channel.RequeueAsync(receivedMessage);

        receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        await _channel.RequeueAsync(receivedMessage);

        receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        await _channel.RequeueAsync(receivedMessage);

        await Task.Delay(5000);

        int dlqCount = await GetDLQCountAsync();
        await Assert.That(dlqCount).IsEqualTo(1);
    }

    private async Task<int> GetDLQCountAsync()
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(_deadLetterChannel);
        var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageAttributeNames = new List<string> { "All", "ApproximateReceiveCount" }
        });

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {_deadLetterChannel.Value}. Response: {response.HttpStatusCode}");
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
