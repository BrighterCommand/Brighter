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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Tasks;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

/// <summary>
/// The <see cref="ChannelFactory"/> class is responsible for creating and managing SQS channels.
/// </summary>
public class ChannelFactory : AWSMessagingGateway, IAmAChannelFactory
{
    private readonly SqsMessageConsumerFactory _messageConsumerFactory;
    private SqsSubscription? _subscription;
    private string? _queueUrl;
    private string? _dlqARN;
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
            .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            });
    }

    /// <summary>
    /// Creates the input channel.
    /// Sync over Async is used here; should be alright in context of channel creation.
    /// </summary>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <returns>An instance of <see cref="IAmAChannelSync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public IAmAChannelSync CreateSyncChannel(Subscription subscription) => BrighterSynchronizationHelper.Run(async () => await CreateSyncChannelAsync(subscription));
        
    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <remarks>
    /// Sync over Async is used here; should be alright in context of channel creation.
    /// </remarks>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription) => BrighterSynchronizationHelper.Run(async () => await CreateAsyncChannelAsync(subscription));

    /// <summary>
    /// Creates the input channel.
    /// </summary>
    /// <param name="subscription">An SqsSubscription, the subscription parameter to create the channel with.</param>
    /// <param name="ct">Cancels the creation operation</param>
    /// <returns>An instance of <see cref="IAmAChannelAsync"/>.</returns>
    /// <exception cref="ConfigurationException">Thrown when the subscription is not an SqsSubscription.</exception>
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        var channel = await _retryPolicy.ExecuteAsync(async () =>
        {
            SqsSubscription? sqsSubscription = subscription as SqsSubscription;
            _subscription = sqsSubscription ?? throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");

            await EnsureTopicAsync(_subscription.RoutingKey, _subscription.FindTopicBy, _subscription.SnsAttributes, _subscription.MakeChannels);
            await EnsureQueueAsync();

            return new ChannelAsync(
                subscription.ChannelName.ToValidSQSQueueName(),
                subscription.RoutingKey.ToValidSNSTopicName(),
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

        using var sqsClient = new AmazonSQSClient(AwsConnection.Credentials, AwsConnection.Region);
        (bool exists, string? queueUrl) queueExists = await QueueExistsAsync(sqsClient, _subscription.ChannelName.ToValidSQSQueueName());

        if (queueExists.exists && queueExists.queueUrl != null)
        {
            try
            {
                sqsClient.DeleteQueueAsync(queueExists.queueUrl)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception)
            {
                s_logger.LogError("Could not delete queue {ChannelName}", queueExists.queueUrl);
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

        using var snsClient = new AmazonSimpleNotificationServiceClient(AwsConnection.Credentials, AwsConnection.Region);
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
                s_logger.LogError("Could not delete topic {TopicResourceName}", ChannelTopicArn);
            }
        }
    }
        
    private async Task<IAmAChannelSync> CreateSyncChannelAsync(Subscription subscription)
    {
        var channel = await _retryPolicy.ExecuteAsync(async () =>
        {
            SqsSubscription? sqsSubscription = subscription as SqsSubscription;
            _subscription = sqsSubscription ?? throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");

            await EnsureTopicAsync(_subscription.RoutingKey, _subscription.FindTopicBy, _subscription.SnsAttributes,
                _subscription.MakeChannels);
            await EnsureQueueAsync();

            return new Channel(
                subscription.ChannelName.ToValidSQSQueueName(),
                subscription.RoutingKey.ToValidSNSTopicName(),
                _messageConsumerFactory.Create(subscription),
                subscription.BufferSize
            );
        });

        return channel;
    }
  
    private async Task EnsureQueueAsync()
    {
        if (_subscription is null)
            throw new InvalidOperationException("ChannelFactory: Subscription cannot be null");
            
        if (_subscription.MakeChannels == OnMissingChannel.Assume)
            return;

        using var sqsClient = new AmazonSQSClient(AwsConnection.Credentials, AwsConnection.Region);
        var queueName = _subscription.ChannelName.ToValidSQSQueueName();
        var topicName = _subscription.RoutingKey.ToValidSNSTopicName();

        (bool exists, _) = await QueueExistsAsync(sqsClient, queueName);
        if (!exists)
        {
            if (_subscription.MakeChannels == OnMissingChannel.Create)
            {
                if (_subscription.RedrivePolicy != null)
                {
                    await CreateDLQAsync(sqsClient);
                }

                await CreateQueueAsync(sqsClient);
            }
            else if (_subscription.MakeChannels == OnMissingChannel.Validate)
            {
                var message = $"Queue does not exist: {queueName} for {topicName} on {AwsConnection.Region}";
                s_logger.LogDebug("Queue does not exist: {ChannelName} for {Topic} on {Region}", queueName, topicName, AwsConnection.Region);
                throw new QueueDoesNotExistException(message);
            }
        }
        else
        {
            s_logger.LogDebug("Queue exists: {ChannelName} subscribed to {Topic} on {Region}", queueName, topicName, AwsConnection.Region);
        }
    }

    private async Task CreateQueueAsync(AmazonSQSClient sqsClient)
    {
        if (_subscription is null)
            throw new InvalidOperationException("ChannelFactory: Subscription cannot be null");
            
        s_logger.LogDebug("Queue does not exist, creating queue: {ChannelName} subscribed to {Topic} on {Region}", _subscription.ChannelName.Value, _subscription.RoutingKey.Value, AwsConnection.Region);
        _queueUrl = null;
        try
        {
            var attributes = new Dictionary<string, string>();
            if (_subscription.RedrivePolicy != null && _dlqARN != null)
            {
                var policy = new { maxReceiveCount = _subscription.RedrivePolicy.MaxReceiveCount, deadLetterTargetArn = _dlqARN };
                attributes.Add("RedrivePolicy", JsonSerializer.Serialize(policy, JsonSerialisationOptions.Options));
            }

            attributes.Add("DelaySeconds", _subscription.DelaySeconds.ToString());
            attributes.Add("MessageRetentionPeriod", _subscription.MessageRetentionPeriod.ToString());
            if (_subscription.IAMPolicy != null) attributes.Add("Policy", _subscription.IAMPolicy);
            attributes.Add("ReceiveMessageWaitTimeSeconds", _subscription.TimeOut.Seconds.ToString());
            attributes.Add("VisibilityTimeout", _subscription.LockTimeout.ToString());

            var tags = new Dictionary<string, string> { { "Source", "Brighter" } };
            if (_subscription.Tags != null)
            {
                foreach (var tag in _subscription.Tags)
                {
                    tags.Add(tag.Key, tag.Value);
                }
            }

            var request = new CreateQueueRequest(_subscription.ChannelName.Value)
            {
                Attributes = attributes,
                Tags = tags
            };
            var response = await sqsClient.CreateQueueAsync(request);
            _queueUrl = response.QueueUrl;

            if (!string.IsNullOrEmpty(_queueUrl))
            {
                s_logger.LogDebug("Queue created: {URL}", _queueUrl);
                using var snsClient = new AmazonSimpleNotificationServiceClient(AwsConnection.Credentials, AwsConnection.Region);
                await CheckSubscriptionAsync(_subscription.MakeChannels, sqsClient, snsClient);
            }
            else
            {
                throw new InvalidOperationException($"Could not create queue: {_subscription.ChannelName.Value} subscribed to {ChannelTopicArn} on {AwsConnection.Region}");
            }
        }
        catch (QueueDeletedRecentlyException ex)
        {
            var error = $"Could not create queue {_subscription.ChannelName.Value} because {ex.Message} waiting 60s to retry";
            s_logger.LogError(ex, "Could not create queue {ChannelName} because {ErrorMessage} waiting 60s to retry", _subscription.ChannelName.Value, ex.Message);
            Thread.Sleep(TimeSpan.FromSeconds(30));
            throw new ChannelFailureException(error, ex);
        }
        catch (AmazonSQSException ex)
        {
            var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {AwsConnection.Region.DisplayName} because {ex.Message}";
            s_logger.LogError(ex, "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}", _queueUrl, _subscription.RoutingKey.Value, AwsConnection.Region.DisplayName, ex.Message);
            throw new InvalidOperationException(error, ex);
        }
        catch (HttpErrorResponseException ex)
        {
            var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {AwsConnection.Region.DisplayName} because {ex.Message}";
            s_logger.LogError(ex, "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}", _queueUrl, _subscription.RoutingKey.Value, AwsConnection.Region.DisplayName, ex.Message);
            throw new InvalidOperationException(error, ex);
        }
    }

    private async Task CreateDLQAsync(AmazonSQSClient sqsClient)
    {
        if (_subscription is null)
            throw new InvalidOperationException("ChannelFactory: Subscription cannot be null");
            
        if (_subscription.RedrivePolicy == null)
            throw new InvalidOperationException("ChannelFactory: RedrivePolicy cannot be null when creating a DLQ");
            
        try
        {
            var request = new CreateQueueRequest(_subscription.RedrivePolicy.DeadlLetterQueueName.Value);
            var createDeadLetterQueueResponse = await sqsClient.CreateQueueAsync(request);
            var queueUrl = createDeadLetterQueueResponse.QueueUrl;

            if (!string.IsNullOrEmpty(queueUrl))
            {
                var attributesRequest = new GetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,
                    AttributeNames = ["QueueArn"]
                };
                var attributesResponse = await sqsClient.GetQueueAttributesAsync(attributesRequest);

                if (attributesResponse.HttpStatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException($"Could not find ARN of DLQ, status: {attributesResponse.HttpStatusCode}");

                _dlqARN = attributesResponse.QueueARN;
            }
            else
                throw new InvalidOperationException($"Could not find create DLQ, status: {createDeadLetterQueueResponse.HttpStatusCode}");
        }
        catch (QueueDeletedRecentlyException ex)
        {
            var error = $"Could not create queue {_subscription.ChannelName.Value} because {ex.Message} waiting 60s to retry";
            s_logger.LogError(ex, "Could not create queue {ChannelName} because {ErrorMessage} waiting 60s to retry", _subscription.ChannelName.Value, ex.Message);
            Thread.Sleep(TimeSpan.FromSeconds(30));
            throw new ChannelFailureException(error, ex);
        }
        catch (AmazonSQSException ex)
        {
            var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {AwsConnection.Region.DisplayName} because {ex.Message}";
            s_logger.LogError(ex, "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}", _queueUrl, _subscription.RoutingKey.Value, AwsConnection.Region.DisplayName, ex.Message);
            throw new InvalidOperationException(error, ex);
        }
        catch (HttpErrorResponseException ex)
        {
            var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {AwsConnection.Region.DisplayName} because {ex.Message}";
            s_logger.LogError(ex, "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}", _queueUrl, _subscription.RoutingKey.Value, AwsConnection.Region.DisplayName, ex.Message);
            throw new InvalidOperationException(error, ex);
        }
    }

    private async Task CheckSubscriptionAsync(OnMissingChannel makeSubscriptions, AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
    {
        if (makeSubscriptions == OnMissingChannel.Assume)
            return;

        if (!await SubscriptionExistsAsync(sqsClient, snsClient))
        {
            if (makeSubscriptions == OnMissingChannel.Validate)
            {
                throw new BrokerUnreachableException($"Subscription validation error: could not find subscription for {_queueUrl}");
            }
            else if (makeSubscriptions == OnMissingChannel.Create)
            {
                await SubscribeToTopicAsync(sqsClient, snsClient);
            }
        }
    }

    private async Task SubscribeToTopicAsync(AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
    {
        var arn = await snsClient.SubscribeQueueAsync(ChannelTopicArn, sqsClient, _queueUrl);
        if (!string.IsNullOrEmpty(arn))
        {
            var response = await snsClient.SetSubscriptionAttributesAsync(
                new SetSubscriptionAttributesRequest(arn, "RawMessageDelivery", _subscription?.RawMessageDelivery.ToString())
            );
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException("Unable to set subscription attribute for raw message delivery");
            }
        }
        else
        {
            throw new InvalidOperationException($"Could not subscribe to topic: {ChannelTopicArn} from queue: {_queueUrl} in region {AwsConnection.Region}");
        }
    }

    private async Task<(bool exists, string? queueUrl)> QueueExistsAsync(AmazonSQSClient client, string? channelName)
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

        return (exists, queueUrl);
    }

    private async Task<bool> SubscriptionExistsAsync(AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
    {
        string? queueArn = await GetQueueArnForChannelAsync(sqsClient);

        if (queueArn == null)
            throw new BrokerUnreachableException($"Could not find queue ARN for queue {_queueUrl}");

        bool exists = false;
        ListSubscriptionsByTopicResponse response;
        do
        {
            response = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest { TopicArn = ChannelTopicArn });
            exists = response.Subscriptions.Any(sub => (sub.Protocol.ToLower() == "sqs") && (sub.Endpoint == queueArn));
        } while (!exists && response.NextToken != null);

        return exists;
    }

    /// <summary>
    /// Gets the ARN of the queue for the channel.
    /// Sync over async is used here; should be alright in context of channel creation.
    /// </summary>
    /// <param name="sqsClient">The SQS client.</param>
    /// <returns>The ARN of the queue.</returns>
    private async Task<string?> GetQueueArnForChannelAsync(AmazonSQSClient sqsClient)
    {
        var result = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest { QueueUrl = _queueUrl, AttributeNames = new List<string> { "QueueArn" } }
        );

        if (result.HttpStatusCode == HttpStatusCode.OK)
        {
            return result.QueueARN;
        }

        return null;
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
            response = await snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest { TopicArn = ChannelTopicArn });
            foreach (var sub in response.Subscriptions)
            {
                var unsubscribe = await snsClient.UnsubscribeAsync(new UnsubscribeRequest { SubscriptionArn = sub.SubscriptionArn });
                if (unsubscribe.HttpStatusCode != HttpStatusCode.OK)
                {
                    s_logger.LogError("Error unsubscribing from {TopicResourceName} for sub {ChannelResourceName}", ChannelTopicArn, sub.SubscriptionArn);
                }
            }
        } while (response.NextToken != null);
    }
}
