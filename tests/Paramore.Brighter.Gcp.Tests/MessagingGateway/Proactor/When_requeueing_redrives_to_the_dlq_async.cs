using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Proactor;

[Trait("Category", "GCP")]
[Trait("Fragile", "CI")]
public class MessageProducerDlqTestsAsync : IDisposable
{
    private const int MaxDeliveryAttempts = 5;
    private readonly GcpMessageProducer _sender;
    private readonly IAmAChannelAsync _channel;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly Message _message;
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpSubscription<MyCommand> _subscription;

    public MessageProducerDlqTestsAsync()
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
            messagePumpType: MessagePumpType.Proactor,
            ackDeadlineSeconds: 60,
            deadLetter: new DeadLetterPolicy($"DLQ-Requeue-Test-{Guid.NewGuid().ToString()}", dlQueue)
            {
                AckDeadlineSeconds = 60,
                MaxDeliveryAttempts = MaxDeliveryAttempts
            },
            makeChannels: OnMissingChannel.Create
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
        _channel = _channelFactory.CreateAsyncChannel(_subscription);
    }

    [Fact]
    public async Task When_requeueing_redrives_to_the_queue_async()
    {
        await _sender.SendAsync(_message);
        for (var i = 0; i <= MaxDeliveryAttempts; i++)
        {
            var receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
            await _channel.RequeueAsync(receivedMessage);
        }
        
        await Task.Delay(5000);

        int dlqCount = await GetDLQCountAsync();
        Assert.Equal(1, dlqCount);
    }

    private async Task<int> GetDLQCountAsync()
    {
        var client = await _connection.CreateSubscriberServiceApiClientAsync();
        var subName = Google.Cloud.PubSub.V1.SubscriptionName.FormatProjectSubscription(_connection.ProjectId,
            _subscription.DeadLetter!.Subscription!);
        var messages = await client.PullAsync(new PullRequest
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
