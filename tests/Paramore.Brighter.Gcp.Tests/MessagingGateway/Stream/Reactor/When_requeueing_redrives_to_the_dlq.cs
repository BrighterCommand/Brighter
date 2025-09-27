using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Reactor;

[Trait("Category", "GCP")]
public class StreamMessageProducerDlqTestsAsync : IDisposable
{
    private const int MaxDeliveryAttempts = 5;
    private readonly GcpMessageProducer _sender;
    private readonly IAmAChannelSync _channel;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly Message _message;
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpSubscription<MyCommand> _subscription;

    public StreamMessageProducerDlqTestsAsync()
    {
        const string replyTo = "http:\\queueUrl";
        MyCommand myCommand = new() { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);  
        var queueName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var dlQueue = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-DLQ-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        
         _subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            ackDeadlineSeconds: 60,
            deadLetter: new DeadLetterPolicy($"DLQ-Requeue-Test-{Guid.NewGuid().ToString()}", dlQueue)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = MaxDeliveryAttempts
            },
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        _connection = GatewayFactory.CreateFactory();

        _sender = new GcpMessageProducer(_connection, 
            new GcpPublication
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Create
            });

        _channelFactory = new GcpPubSubChannelFactory(_connection);
        _channel = _channelFactory.CreateSyncChannel(_subscription);
    }

    [Fact]
    public void When_requeueing_redrives_to_the_queue()
    {
        _sender.Send(_message);
        for (var i = 0; i <= MaxDeliveryAttempts; i++)
        {
            var receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            _channel.Requeue(receivedMessage);
        }
        
        Thread.Sleep(5000);

        var dlqCount = GetDLQCount();
        Assert.Equal(1, dlqCount);
    }

    private int GetDLQCount()
    {
        var client = _connection.CreateSubscriberServiceApiClient();
        var subName = Google.Cloud.PubSub.V1.SubscriptionName.FormatProjectSubscription(_connection.ProjectId,
            _subscription.DeadLetter!.Subscription!);
        var messages = client.Pull(new PullRequest
        {
            MaxMessages = 10, 
            Subscription = subName
        });
        
       return messages.ReceivedMessages.Count;
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopic(_subscription);
        _channelFactory.DeleteSubscription(_subscription);
    }
}
