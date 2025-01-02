﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Standard;

[Trait("Category", "AWS")]
public class CustomisingAwsClientConfigTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public CustomisingAwsClientConfigTestsAsync()
    {
        MyCommand myCommand = new() { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        const string contentType = "text\\plain";
        string correlationId = Guid.NewGuid().ToString();
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            messagePumpType: MessagePumpType.Proactor,
            routingKey: routingKey
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var subscribeAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory =
                new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("async_sub"));
        });

        _channelFactory = new ChannelFactory(subscribeAwsConnection);
        _channel = _channelFactory.CreateAsyncChannel(subscription);

        var publishAwsConnection = GatewayFactory.CreateFactory(config =>
        {
            config.HttpClientFactory =
                new InterceptingHttpClientFactory(new InterceptingDelegatingHandler("async_pub"));
        });

        _messageProducer = new SnsMessageProducer(publishAwsConnection,
            new SnsPublication { Topic = new RoutingKey(topicName), MakeChannels = OnMissingChannel.Create });
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
        InterceptingDelegatingHandler.RequestCount.Should().ContainKey("async_sub");
        InterceptingDelegatingHandler.RequestCount["async_sub"].Should().BeGreaterThan(0);

        InterceptingDelegatingHandler.RequestCount.Should().ContainKey("async_pub");
        InterceptingDelegatingHandler.RequestCount["async_pub"].Should().BeGreaterThan(0);
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
