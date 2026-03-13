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

public class SnsFifoMessageGatewayProvider
    : SnsFifo.Proactor.IAmAMessageGatewayProactorProvider,
      SnsFifo.Reactor.IAmAMessageGatewayReactorProvider
{
    private static readonly TimeSpan s_sqsMinTimeout = TimeSpan.FromSeconds(5);
    private readonly AWSMessagingGatewayConnection _awsConnection;

    public SnsFifoMessageGatewayProvider()
    {
        _awsConnection = GatewayFactory.CreateFactory();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"sns-fifo-{Uuid.New():N}.fifo");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"sns-fifo-ch-{Uuid.New():N}.fifo");
    }

    public SnsPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new SnsPublication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
            TopicAttributes = new SnsAttributes { Type = SqsType.Fifo },
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
            var deadLetterChannelName = new ChannelName($"{channelName.Value.Replace(".fifo", "")}-dlq.fifo");
            return new SqsSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                channelType: ChannelType.PubSub,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannel,
                queueAttributes: new SqsAttributes(
                    type: SqsType.Fifo,
                    redrivePolicy: new RedrivePolicy(deadLetterChannelName, 3)
                ),
                topicAttributes: new SnsAttributes { Type = SqsType.Fifo },
                deadLetterRoutingKey: new RoutingKey(deadLetterChannelName),
                requeueCount: 3
            );
        }

        return new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            queueAttributes: new SqsAttributes(type: SqsType.Fifo),
            topicAttributes: new SnsAttributes { Type = SqsType.Fifo }
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

    public IAmAMessageProducerSync CreateProducer(SnsPublication publication)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SnsMessageProducer(connection, publication);
        return producer;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        SnsPublication publication,
        CancellationToken cancellationToken = default)
    {
        var connection = _awsConnection;

        if (publication.MakeChannels == OnMissingChannel.Validate)
        {
            connection = GatewayFactory.CreateFactory();
        }

        var producer = new SnsMessageProducer(connection, publication);
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
