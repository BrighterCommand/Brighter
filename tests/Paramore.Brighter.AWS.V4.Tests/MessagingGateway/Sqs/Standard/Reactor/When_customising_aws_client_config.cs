using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Category("AWS")]
public class CustomisingAwsClientConfigTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public CustomisingAwsClientConfigTests()
    {
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        MyCommand myCommand = new() { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var channelName = new ChannelName(queueName);
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint, routingKey: routingKey, messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var subscribeAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory = new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sqs_sync_sub"));
        });

        _channelFactory = new ChannelFactory(subscribeAwsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        var publishAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory = new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sqs_sync_pub"));
        });

        _messageProducer = new SqsMessageProducer(publishAwsConnection,
            new SqsPublication { ChannelName = channelName, MakeChannels = OnMissingChannel.Create });
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
        await Assert.That(InterceptingDelegatingHandler.RequestCount).ContainsKey("sqs_sync_sub");
        await Assert.That((InterceptingDelegatingHandler.RequestCount["sqs_sync_sub"]) > (0)).IsTrue();
        
        await Assert.That(InterceptingDelegatingHandler.RequestCount).ContainsKey("sqs_sync_pub");
        await Assert.That((InterceptingDelegatingHandler.RequestCount["sqs_sync_pub"]) > (0)).IsTrue();
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
