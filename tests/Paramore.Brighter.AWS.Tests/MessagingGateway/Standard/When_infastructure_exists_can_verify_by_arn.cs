﻿using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Standard;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class AWSValidateInfrastructureByArnTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AWSValidateInfrastructureByArnTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        string contentType = "text\\plain";
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey($"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        SqsSubscription<MyCommand> subscription = new(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );


        (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
        var awsConnection = GatewayFactory.CreateFactory(credentials, region);

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateSyncChannel(subscription);

        var topicArn = FindTopicArn(awsConnection, routingKey.Value);
        var routingKeyArn = new RoutingKey(topicArn);

        //Now change the subscription to validate, just check what we made
        subscription = new(
            name: new SubscriptionName(channelName),
            channelName: channel.Name,
            routingKey: routingKeyArn,
            findTopicBy: TopicFindBy.Arn,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate
        );

        _messageProducer = new SnsMessageProducer(
            awsConnection,
            new SnsPublication
            {
                Topic = routingKey,
                TopicArn = topicArn,
                FindTopicBy = TopicFindBy.Arn,
                MakeChannels = OnMissingChannel.Validate
            });

        _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);
    }

    [Fact]
    public async Task When_infrastructure_exists_can_verify()
    {
        //arrange
        _messageProducer.Send(_message);

        await Task.Delay(1000);

        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        message.Id.Should().Be(_myCommand.Id);

        //clear the queue
        _consumer.Acknowledge(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _consumer.Dispose();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageConsumerAsync)_consumer).DisposeAsync();
        await _messageProducer.DisposeAsync();
    }

    private static string FindTopicArn(AWSMessagingGatewayConnection connection, string topicName)
    {
        using var snsClient = new AWSClientFactory(connection).CreateSnsClient();
        var topicResponse = snsClient.FindTopicAsync(topicName).GetAwaiter().GetResult();
        return topicResponse.TopicArn;
    }
}