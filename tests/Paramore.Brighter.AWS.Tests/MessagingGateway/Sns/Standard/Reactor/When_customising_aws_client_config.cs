using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Reactor;

[Category("AWS")]
public class CustomisingAwsClientConfigTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public CustomisingAwsClientConfigTests()
    {
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        MyCommand myCommand = new() { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var subscribeAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory = new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sync_sub"));
        });

        _channelFactory = new ChannelFactory(subscribeAwsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        var publishAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory = new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sync_pub"));
        });

        _messageProducer = new SnsMessageProducer(
            publishAwsConnection,
            new SnsPublication { Topic = new RoutingKey(topicName), 
                MakeChannels = OnMissingChannel.Create });
    }

    [Test]
    public async Task When_customising_aws_client_config()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //clear the queue
        _channel.Acknowledge(message);

        //publish_and_subscribe_should_use_custom_http_client_factory
        await Assert.That(InterceptingDelegatingHandler.RequestCount).ContainsKey("sync_sub");
        await Assert.That((InterceptingDelegatingHandler.RequestCount["sync_sub"]) > (0)).IsTrue();
        
        await Assert.That(InterceptingDelegatingHandler.RequestCount).ContainsKey("sync_pub");
        await Assert.That((InterceptingDelegatingHandler.RequestCount["sync_pub"]) > (0)).IsTrue();
    }

    [After(Test)]
    public async Task Cleanup()
    {
        //Clean up resources that we have created
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
