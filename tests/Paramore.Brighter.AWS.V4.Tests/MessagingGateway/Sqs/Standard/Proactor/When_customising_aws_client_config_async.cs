using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Trait("Category", "AWS")]
public class CustomisingAwsClientConfigTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public CustomisingAwsClientConfigTestsAsync()
    {
        MyCommand myCommand = new() { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        string correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint, 
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
            );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var subscribeAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory =
                new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sqs_async_sub"));
        });

        _channelFactory = new ChannelFactory(subscribeAwsConnection);
        _channel = _channelFactory.CreateAsyncChannel(subscription);

        var publishAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory =
                new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("sqs_async_pub"));
        });

        _messageProducer = new SqsMessageProducer(publishAwsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create)
            );
    }

    [Fact]
    public async Task When_customising_aws_client_config()
    {
        //arrange
        await _messageProducer.SendAsync(_message);

        await Task.Delay(1000);

        var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        //clear the queue
        await _channel.AcknowledgeAsync(message);

        //publish_and_subscribe_should_use_custom_http_client_factory
        Assert.Contains("sqs_async_sub", InterceptingDelegatingHandler.RequestCount);
        Assert.True((InterceptingDelegatingHandler.RequestCount["sqs_async_sub"]) > (0));

        Assert.Contains("sqs_async_pub", InterceptingDelegatingHandler.RequestCount);
        Assert.True((InterceptingDelegatingHandler.RequestCount["sqs_async_pub"]) > (0));
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
