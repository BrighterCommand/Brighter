﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")]
public class CustomisingAwsClientConfigTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public CustomisingAwsClientConfigTests()
    {
        const string replyTo = "http:\\queueUrl";
        const string contentType = "text\\plain";
        MyCommand myCommand = new() { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            messagePumpType: MessagePumpType.Reactor,
            routingKey: routingKey,
            channelType: ChannelType.PointToPoint
        );

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
            new SqsPublication { Topic = new RoutingKey(queueName), MakeChannels = OnMissingChannel.Create });
    }

    [Fact]
    public async Task When_customising_aws_client_config()
    {
        //arrange
        _messageProducer.Send(_message);

        await Task.Delay(1000);

        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //clear the queue
        _channel.Acknowledge(message);

        //publish_and_subscribe_should_use_custom_http_client_factory
        InterceptingDelegatingHandler.RequestCount.Should().ContainKey("sqs_sync_sub");
        InterceptingDelegatingHandler.RequestCount["sqs_sync_sub"].Should().BeGreaterThan(0);
        
        InterceptingDelegatingHandler.RequestCount.Should().ContainKey("sqs_sync_pub");
        InterceptingDelegatingHandler.RequestCount["sqs_sync_pub"].Should().BeGreaterThan(0);
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