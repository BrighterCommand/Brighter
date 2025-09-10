#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Tasks;
using Polly;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// The <see cref="ChannelFactory"/> class is responsible for creating and managing SNS/SQS channels.
/// </summary>
public partial class ChannelFactory : AwsMessagingGateway, IAmAChannelFactory
{
    private readonly SqsMessageConsumerFactory _messageConsumerFactory;
    private SqsSubscription? _subscription;
    private readonly AsyncRetryPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
    /// </summary>
    /// <param name="awsConnection">The details of the subscription to AWS.</param>
    public ChannelFactory(AWSMessagingGatewayConnection awsConnection)
        : base(awsConnection)
    {
        _messageConsumerFactory = new SqsMessageConsumerFactory(awsConnection);
        _retryPolicy = Policy
            .Handle<InvalidOperationException>()
            .WaitAndRetryAsync([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)]);
    }

    /// <summary>
    /// Creates the input channel.
    /// Sync over Async is used here; should be alright in context of channel creation.
    /// </summary>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <returns>An instance of <see cref="IAmAChannelSync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription) =>
        BrighterAsyncContext.Run(async () => await CreateSyncChannelAsync(subscription));

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <remarks>
    /// Sync over Async is used here; should be alright in context of channel creation.
    /// </remarks>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription) =>
        BrighterAsyncContext.Run(async () => await CreateAsyncChannelAsync(subscription));

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <param name="ct">Cancels the creation operation</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(
        Subscription subscription,
        CancellationToken ct = default)
    {
        var channel = await _retryPolicy.ExecuteAsync(async () =>
        {
            SqsSubscription? sqsSubscription = subscription as SqsSubscription;
            _subscription = sqsSubscription ??
                            throw new ConfigurationException(
                                "We expect an SqsSubscription or SqsSubscription<T> as a parameter");


            var isFifo = _subscription.QueueAttributes.Type == SqsType.Fifo;
            var queueName = _subscription.ChannelName.Value.ToValidSQSQueueName(isFifo);
            
            //on assume, don't try to create the queue or topic, just return a channel
            if (_subscription.MakeChannels == OnMissingChannel.Assume)
                return new ChannelAsync(
                    new ChannelName(queueName),
                    new RoutingKey(_subscription.RoutingKey),
                    _messageConsumerFactory.CreateAsync(subscription),
                    subscription.BufferSize
                ); 
            
            var queueUrl =await EnsureQueueAsync(
                queueName,
                _subscription.ChannelType,
                _subscription.FindQueueBy,
                _subscription.QueueAttributes,
                _subscription.MakeChannels,
                ct);
            
            //Either we found it, or we made it, so we should have a queueUrl
            if (queueUrl is null)
                throw new InvalidOperationException($"ChannelFactory.CreateAsyncChannelAsync: Could not create queue {queueName}");
            
            var routingKey = RoutingKey.Empty;
            if (_subscription.ChannelType == ChannelType.PubSub)
                routingKey = await EnsureSubscriptionAsync(
                    isFifo, 
                    queueUrl, 
                    _subscription.RoutingKey,
                    _subscription.FindTopicBy,
                    _subscription.TopicAttributes,
                    _subscription.QueueAttributes,
                    _subscription.MakeChannels,
                    ct);

            return new ChannelAsync(
                new ChannelName(queueName),
                new RoutingKey(routingKey),
                _messageConsumerFactory.CreateAsync(subscription),
                subscription.BufferSize
            );
        });

        return channel;
    }

    /// <summary>
    /// Deletes the queue.
    /// </summary>
    public async Task DeleteQueueAsync()
    {
        if (_subscription?.ChannelName is null)
            return;

        using var sqsClient = new AWSClientFactory(AwsConnection).CreateSqsClient();
        (bool exists, string? queueUrl) queueExists =
            await QueueExistsAsync(sqsClient,
                _subscription.ChannelName.ToValidSQSQueueName(_subscription.QueueAttributes.Type == SqsType.Fifo));

        if (queueExists is { exists: true, queueUrl: not null })
        {
            try
            {
                await sqsClient.DeleteQueueAsync(queueExists.queueUrl);
            }
            catch (Exception)
            {
                Log.CouldNotDeleteQueue(s_logger, queueExists.queueUrl);
            }
        }
    }

    /// <summary>
    /// Deletes the topic.
    /// </summary>
    public async Task DeleteTopicAsync()
    {
        if (_subscription == null)
            return;

        if (ChannelTopicArn == null)
            return;

        using var snsClient = new AWSClientFactory(AwsConnection).CreateSnsClient();
        (bool exists, string? _) = await new ValidateTopicByArn(snsClient).ValidateAsync(ChannelTopicArn);
        if (exists)
        {
            try
            {
                await UnsubscribeFromTopicAsync(snsClient);
                await snsClient.DeleteTopicAsync(ChannelTopicArn);
            }
            catch (Exception)
            {
                Log.CouldNotDeleteTopic(s_logger, ChannelTopicArn);
            }
        }
    }

    private async Task<IAmAChannelSync> CreateSyncChannelAsync(Subscription subscription)
    {
        var channel = await _retryPolicy.ExecuteAsync(async () =>
        {
            SqsSubscription? sqsSubscription = subscription as SqsSubscription;
            _subscription = sqsSubscription ??
                            throw new ConfigurationException(
                                "We expect an SqsSubscription or SqsSubscription<T> as a parameter");

            var isFifo = _subscription.QueueAttributes.Type == SqsType.Fifo;
            var queueName = _subscription.ChannelName.Value.ToValidSQSQueueName(isFifo);
            
            //on assume, don't try to create the queue or topic, just return a channel
            if (_subscription.MakeChannels == OnMissingChannel.Assume)
                return new Channel(
                    new ChannelName(queueName),
                    new RoutingKey(_subscription.RoutingKey),
                    _messageConsumerFactory.Create(subscription),
                    subscription.BufferSize
                ); 

            var queueUrl = await EnsureQueueAsync(
                queueName,
                _subscription.ChannelType,
                _subscription.FindQueueBy,
                _subscription.QueueAttributes,
                _subscription.MakeChannels);
            
            //Either we found it, or we made it, so we should have a queueUrl
            if (queueUrl is null)
                throw new InvalidOperationException($"ChannelFactory.CreateAsyncChannelAsync: Could not create queue {queueName}");
            
            var routingKey = RoutingKey.Empty;
            if (_subscription.ChannelType == ChannelType.PubSub)
                routingKey = await EnsureSubscriptionAsync(
                    isFifo, 
                    queueUrl, 
                    _subscription.RoutingKey,
                    _subscription.FindTopicBy,
                    _subscription.TopicAttributes,
                    _subscription.QueueAttributes,
                    _subscription.MakeChannels
                );

            return new Channel(
                new ChannelName(queueName),
                new RoutingKey(routingKey),
                _messageConsumerFactory.Create(subscription),
                subscription.BufferSize
            );
        });

        return channel;
    }

    private static async Task<(bool exists, string? queueUrl)> QueueExistsAsync(AmazonSQSClient client, string? channelName)
    {
        if (string.IsNullOrEmpty(channelName))
            return (false, null);

        bool exists = false;
        string? queueUrl = null;
        try
        {
            var response = await client.GetQueueUrlAsync(channelName);
            if (!string.IsNullOrWhiteSpace(response.QueueUrl))
            {
                queueUrl = response.QueueUrl;
                exists = true;
            }
        }
        catch (AggregateException ae)
        {
            ae.Handle((e) =>
            {
                if (e is QueueDoesNotExistException)
                {
                    exists = false;
                    return true;
                }

                return false;
            });
        }
        catch (QueueDoesNotExistException)
        {
            exists = false;
        }

        return (exists, queueUrl);
    }

    /// <summary>
    /// Unsubscribes from the topic.
    /// Sync over async is used here; should be alright in context of topic unsubscribe.
    /// </summary>
    /// <param name="snsClient">The SNS client.</param>
    private async Task UnsubscribeFromTopicAsync(AmazonSimpleNotificationServiceClient snsClient)
    {
        ListSubscriptionsByTopicResponse response;
        do
        {
            response = await snsClient.ListSubscriptionsByTopicAsync(
                new ListSubscriptionsByTopicRequest { TopicArn = ChannelAddress });
            foreach (var sub in response.Subscriptions)
            {
                var unsubscribe =
                    await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = sub.SubscriptionArn });
                if (unsubscribe.HttpStatusCode != HttpStatusCode.OK)
                {
                    Log.ErrorUnsubscribingFromTopic(s_logger, ChannelAddress, sub.SubscriptionArn);
                }
            }
        } while (response.NextToken != null);
    }


    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Could not delete queue {ChannelName}")]
        public static partial void CouldNotDeleteQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Error, "Could not delete topic {TopicResourceName}")]
        public static partial void CouldNotDeleteTopic(ILogger logger, string topicResourceName);

        [LoggerMessage(LogLevel.Error, "Error unsubscribing from {TopicResourceName} for sub {ChannelResourceName}")]
        public static partial void ErrorUnsubscribingFromTopic(ILogger logger, string? topicResourceName, string channelResourceName);
    }
}
