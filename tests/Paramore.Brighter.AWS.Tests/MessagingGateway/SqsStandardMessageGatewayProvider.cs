using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SqsStandardMessageGatewayProvider
    : SqsStandard.Proactor.IAmAMessageGatewayProactorProvider,
      SqsStandard.Reactor.IAmAMessageGatewayReactorProvider
{
    private static readonly TimeSpan s_sqsMinTimeout = TimeSpan.FromSeconds(5);
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SqsStandardMessageGatewayProvider()
    {
        _awsConnection = GatewayFactory.CreateFactory();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"sqs-std-{Uuid.New():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"sqs-std-ch-{Uuid.New():N}");
    }

    public SqsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SqsPublication
        {
            ChannelName = new ChannelName(routingKey),
            MakeChannels = makeChannels,
        };
    }

    public SqsSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false)
    {
        if (setupDeadLetterQueue)
        {
            var deadLetterChannelName = new ChannelName($"{channelName}-dlq");
            return new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                channelType: ChannelType.PointToPoint,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                queueAttributes: new SqsAttributes(
                    redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)
                ),
                deadLetterRoutingKey: new RoutingKey(deadLetterChannelName),
                requeueCount: 3
            );
        }

        return new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel
        );
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            channel.Purge();
            channel.Dispose();
        }

        producer?.Dispose();
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        if (channel != null)
        {
            await channel.PurgeAsync();
            channel.Dispose();
        }

        if (producer != null)
        {
            await producer.DisposeAsync();
        }
    }

    public IAmAChannelSync CreateChannel(SqsSubscription subscription)
    {
        var channel = new ChannelFactory(_awsConnection)
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        return new MinimumTimeoutChannelSync(channel, s_sqsMinTimeout);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        SqsSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(_awsConnection)
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return new MinimumTimeoutChannelAsync(channel, s_sqsMinTimeout);
    }

    public IAmAMessageProducerSync CreateProducer(SqsPublication publication)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SqsMessageProducer(connection, publication);
        return producer;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        SqsPublication publication,
        CancellationToken cancellationToken = default)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SqsMessageProducer(connection, publication);
        return producer;
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        SqsSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        using var sqsClient = new AWSClientFactory(_awsConnection).CreateSqsClient();
        var queueUrlResponse = await sqsClient.GetQueueUrlAsync(
            subscription.DeadLetterRoutingKey!.Value, cancellationToken);

        for (var i = 0; i < 10; i++)
        {
            var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = queueUrlResponse.QueueUrl,
                WaitTimeSeconds = 5,
                MessageSystemAttributeNames = ["All"],
                MessageAttributeNames = ["All"]
            }, cancellationToken);

            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new AmazonSQSException(
                    $"Failed to receive from DLQ {subscription.DeadLetterRoutingKey!.Value}. Status: {response.HttpStatusCode}");
            }

            if (response.Messages.Count > 0)
            {
                var sqsMsg = response.Messages.First();
                await sqsClient.DeleteMessageAsync(queueUrlResponse.QueueUrl, sqsMsg.ReceiptHandle, cancellationToken);
                return new Message(
                    new MessageHeader(Id.Random(), new RoutingKey(subscription.DeadLetterRoutingKey!.Value), MessageType.MT_EVENT),
                    new MessageBody(sqsMsg.Body));
            }

            await Task.Delay(1000, cancellationToken);
        }

        return new Message();
    }

    public Message GetMessageFromDeadLetterQueue(SqsSubscription subscription)
    {
        return GetMessageFromDeadLetterQueueAsync(subscription).GetAwaiter().GetResult();
    }

}
