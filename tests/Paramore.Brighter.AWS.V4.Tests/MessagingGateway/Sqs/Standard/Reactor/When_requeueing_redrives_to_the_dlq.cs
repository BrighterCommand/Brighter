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
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsMessageProducerDlqTests : IDisposable, IAsyncDisposable
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
        var subscriptionName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        var queueAttributes = new SqsAttributes(
            redrivePolicy: new RedrivePolicy(new ChannelName(_dlqChannelName)!, 2)
        );
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            routingKey: routingKey,
            queueAttributes: queueAttributes,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        //Must have credentials stored in the SDK Credentials store or shared credentials file
        _awsConnection = GatewayFactory.CreateFactory();

        _sender = new SqsMessageProducer(
            _awsConnection, 
            new SqsPublication (channelName: channelName, queueAttributes: queueAttributes, makeChannels: OnMissingChannel.Create )
            );

        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);
    }

    [Fact]
    public void When_requeueing_redrives_to_the_queue()
    {
        _sender.Send(_message);
        var receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        //should force us into the dlq
        receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(receivedMessage);

        Task.Delay(5000);

        //inspect the dlq
        Assert.Equal(1, GetDLQCount(_dlqChannelName));
    }

    private int GetDLQCount(string queueName)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = sqsClient.GetQueueUrlAsync(queueName).GetAwaiter().GetResult();
        var response = sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            WaitTimeSeconds = 5,
            MessageAttributeNames = new List<string> { "All", "ApproximateReceiveCount" }
        }).GetAwaiter().GetResult();

        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            throw new AmazonSQSException(
                $"Failed to GetMessagesAsync for queue {queueName}. Response: {response.HttpStatusCode}");
        }

        return response.Messages.Count;
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
