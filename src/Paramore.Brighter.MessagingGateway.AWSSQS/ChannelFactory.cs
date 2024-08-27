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
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class ChannelFactory : AWSMessagingGateway, IAmAChannelFactory
    {
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;
        private SqsSubscription _subscription;
        private string _queueUrl;
        private string _dlqARN;
        private readonly RetryPolicy _retryPolicy;
        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="awsConnection">The details of the subscription to AWS</param>
        public ChannelFactory(
            AWSMessagingGatewayConnection awsConnection)
            : base(awsConnection)
        {
            _messageConsumerFactory = new SqsMessageConsumerFactory(awsConnection);
            var delay = Backoff.LinearBackoff(TimeSpan.FromSeconds(2), retryCount: 3, factor: 2.0, fastFirst: true);
            _retryPolicy = Policy
                .Handle<InvalidOperationException>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                });
        }

        ///  <summary>
        ///  Creates the input channel.
        ///  With SQS we can ensure that queues exist ahead of creating the consumer, as there is no non-durable queue model
        ///  to create ephemeral queues, nor are there non-mirrored queues (on a single node in the cluster) where nodes
        ///  failing mean we want to create anew as we recreate. So the input factory creates the queue 
        ///  </summary>
        /// <param name="subscription">An SqsSubscription, the subscription parameter so create the channel with</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannel CreateChannel(Subscription subscription)
        {
            var channel = _retryPolicy.Execute(() =>
            {
                SqsSubscription sqsSubscription = subscription as SqsSubscription;
                _subscription = sqsSubscription ?? throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");

                EnsureTopic(_subscription.RoutingKey, _subscription.SnsAttributes, _subscription.FindTopicBy, _subscription.MakeChannels);
                EnsureQueue();

                return new Channel(
                    subscription.ChannelName.ToValidSQSQueueName(), 
                    subscription.RoutingKey.ToValidSNSTopicName(),
                    _messageConsumerFactory.Create(subscription),
                    subscription.BufferSize
                );
            });

            return channel;
        }

        private void EnsureQueue()
        {
            if (_subscription.MakeChannels == OnMissingChannel.Assume)
                return;

            using var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region);
            //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
            var queueName = _subscription.ChannelName.ToValidSQSQueueName();
            var topicName = _subscription.RoutingKey.ToValidSNSTopicName();

            (bool exists, _) = QueueExists(sqsClient, queueName);
            if (!exists)
            {
                if (_subscription.MakeChannels == OnMissingChannel.Create)
                {
                    if (_subscription.RedrivePolicy != null)
                    {
                        CreateDLQ(sqsClient);
                    }
                        
                    CreateQueue(sqsClient);
     
                }
                else if (_subscription.MakeChannels == OnMissingChannel.Validate)
                {
                    var message = $"Queue does not exist: {queueName} for {topicName} on {_awsConnection.Region}";
                    s_logger.LogDebug("Queue does not exist: {ChannelName} for {Topic} on {Region}", queueName,
                        topicName, _awsConnection.Region);
                    throw new QueueDoesNotExistException(message);
                }
            }
            else
            {
                s_logger.LogDebug("Queue exists: {ChannelName} subscribed to {Topic} on {Region}",
                    queueName, topicName, _awsConnection.Region);
            }
        }

        private void CreateQueue(AmazonSQSClient sqsClient)
        {
            s_logger.LogDebug(
                "Queue does not exist, creating queue: {ChannelName} subscribed to {Topic} on {Region}",
                _subscription.ChannelName.Value, _subscription.RoutingKey.Value, _awsConnection.Region);
            _queueUrl = null;
            try
            {
                var attributes = new Dictionary<string, string>();
                if (_subscription.RedrivePolicy != null && _dlqARN != null)
                {
                    var policy = new {maxReceiveCount = _subscription.RedrivePolicy.MaxReceiveCount, deadLetterTargetArn = _dlqARN};
                    attributes.Add("RedrivePolicy", JsonSerializer.Serialize(policy, JsonSerialisationOptions.Options));
                }
                
                attributes.Add("DelaySeconds", _subscription.DelaySeconds.ToString());
                attributes.Add("MessageRetentionPeriod", _subscription.MessageRetentionPeriod.ToString());
                if (_subscription.IAMPolicy != null )attributes.Add("Policy", _subscription.IAMPolicy);
                attributes.Add("ReceiveMessageWaitTimeSeconds", ToSecondsAsString(_subscription.TimeoutInMilliseconds));
                attributes.Add("VisibilityTimeout", _subscription.LockTimeout.ToString());

                var tags = new Dictionary<string, string>();
                tags.Add("Source","Brighter");
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
                var response = sqsClient.CreateQueueAsync(request).GetAwaiter().GetResult();
                _queueUrl = response.QueueUrl;

                if (!string.IsNullOrEmpty(_queueUrl))
                {
                    s_logger.LogDebug("Queue created: {URL}", _queueUrl);
                    using var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region);
                    CheckSubscription(_subscription.MakeChannels, sqsClient, snsClient);
                }
                else
                {
                    throw new InvalidOperationException($"Could not create queue: {_subscription.ChannelName.Value} subscribed to {ChannelTopicArn} on {_awsConnection.Region}");
                }
            }
            catch (QueueDeletedRecentlyException ex)
            {
                //QueueDeletedRecentlyException - wait 30 seconds then retry
                //Although timeout is 60s, we could be partway through that, so apply Copernican Principle 
                //and assume we are halfway through
                var error = $"Could not create queue {_subscription.ChannelName.Value} because {ex.Message} waiting 60s to retry";
                s_logger.LogError(ex, "Could not create queue {ChannelName} because {ErrorMessage} waiting 60s to retry", _subscription.ChannelName.Value, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(30));
                throw new ChannelFailureException(error, ex);
            }
            catch (AmazonSQSException ex)
            {
                var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {_awsConnection.Region.DisplayName} because {ex.Message}";
                s_logger.LogError(ex,
                    "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}",
                    _queueUrl, _subscription.RoutingKey.Value, _awsConnection.Region.DisplayName, ex.Message);
                throw new InvalidOperationException(error, ex);
            }
            catch (HttpErrorResponseException ex)
            {
                var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {_awsConnection.Region.DisplayName} because {ex.Message}";
                s_logger.LogError(ex,
                    "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}",
                    _queueUrl, _subscription.RoutingKey.Value, _awsConnection.Region.DisplayName, ex.Message);
                throw new InvalidOperationException(error, ex);
            }
        }

        private void CreateDLQ(AmazonSQSClient sqsClient)
        {
            try
            {
                var request = new CreateQueueRequest(_subscription.RedrivePolicy.DeadlLetterQueueName.Value);

                var createDeadLetterQueueResponse = sqsClient.CreateQueueAsync(request).GetAwaiter().GetResult();

                var queueUrl = createDeadLetterQueueResponse.QueueUrl;

                if (!string.IsNullOrEmpty(queueUrl))
                {
                    //We need the ARN of the dead letter queue to configure the queue redrive policy, not the name 
                    var attributesRequest = new GetQueueAttributesRequest
                    {
                        QueueUrl = queueUrl, 
                        AttributeNames = new List<string> {"QueueArn"}
                    };
                    var attributesResponse = sqsClient.GetQueueAttributesAsync(attributesRequest).GetAwaiter().GetResult();

                    if (attributesResponse.HttpStatusCode != HttpStatusCode.OK)
                        throw new InvalidOperationException($"Could not find ARN of DLQ, status: {attributesResponse.HttpStatusCode}");

                    _dlqARN = attributesResponse.QueueARN;
                }
                else 
                    throw new InvalidOperationException($"Could not find create DLQ, status: {createDeadLetterQueueResponse.HttpStatusCode}"); 
            }
            catch (QueueDeletedRecentlyException ex)
            {
                //QueueDeletedRecentlyException - wait 30 seconds then retry
                //Although timeout is 60s, we could be partway through that, so apply Copernican Principle 
                //and assume we are halfway through
                var error = $"Could not create queue {_subscription.ChannelName.Value} because {ex.Message} waiting 60s to retry";
                s_logger.LogError(ex,
                    "Could not create queue {ChannelName} because {ErrorMessage} waiting 60s to retry",
                    _subscription.ChannelName.Value, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(30));
                throw new ChannelFailureException(error, ex);
            }
            catch (AmazonSQSException ex)
            {
                var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {_awsConnection.Region.DisplayName} because {ex.Message}";
                s_logger.LogError(ex,
                    "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}",
                    _queueUrl, _subscription.RoutingKey.Value, _awsConnection.Region.DisplayName, ex.Message);
                throw new InvalidOperationException(error, ex);
            }
            catch (HttpErrorResponseException ex)
            {
                var error = $"Could not create queue {_queueUrl} subscribed to topic {_subscription.RoutingKey.Value} in region {_awsConnection.Region.DisplayName} because {ex.Message}";
                s_logger.LogError(ex, "Could not create queue {URL} subscribed to topic {Topic} in region {Region} because {ErrorMessage}",
                    _queueUrl, _subscription.RoutingKey.Value, _awsConnection.Region.DisplayName, ex.Message);
                throw new InvalidOperationException(error, ex);
            }
        }

        private void CheckSubscription(OnMissingChannel makeSubscriptions, AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
        {
            if (makeSubscriptions == OnMissingChannel.Assume)
                return;

            if (!SubscriptionExists(sqsClient, snsClient))
            {
                if (makeSubscriptions == OnMissingChannel.Validate)
                {
                    throw new BrokerUnreachableException($"Subscription validation error: could not find subscription for {_queueUrl}");
                }
                else if (makeSubscriptions == OnMissingChannel.Create)
                {
                    SubscribeToTopic(sqsClient, snsClient);
                }
            }
        }

        private void SubscribeToTopic(AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
        {
            var subscription = snsClient.SubscribeQueueAsync(ChannelTopicArn, sqsClient, _queueUrl).Result;
            if (!string.IsNullOrEmpty(subscription))
            {
                //We need to support raw messages to allow the use of message attributes
                var response = snsClient.SetSubscriptionAttributesAsync(
                        new SetSubscriptionAttributesRequest(
                            subscription, "RawMessageDelivery", _subscription.RawMessageDelivery.ToString())
                    )
                    .Result;
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException("Unable to set subscription attribute for raw message delivery");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Could not subscribe to topic: {ChannelTopicArn} from queue: {_queueUrl} in region {_awsConnection.Region}");
            }
        }

        private string ToSecondsAsString(int timeoutInMilliseconds)
        {
            int timeOutInSeconds = 0;
            if (timeoutInMilliseconds >= 1000)
                timeOutInSeconds = timeoutInMilliseconds / 1000;
            else if (timeoutInMilliseconds > 0)
                timeOutInSeconds = 1;

            return Convert.ToString(timeOutInSeconds);
        }

        private (bool, string) QueueExists(AmazonSQSClient client, string channelName)
        {
            bool exists = false;
            string queueUrl = null;
            try
            {
                var response = client.GetQueueUrlAsync(channelName).Result;
                //If the queue does not exist yet then
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
                        //handle this, because we expect a queue might be missing and will create
                        exists = false;
                        return true;
                    }

                    //we didn't expect this
                    return false;
                });
            }

            return (exists, queueUrl);
        }

        private bool SubscriptionExists(AmazonSQSClient sqsClient, AmazonSimpleNotificationServiceClient snsClient)
        {
            string queueArn = GetQueueARNForChannel(sqsClient);

            if (queueArn == null)
                throw new BrokerUnreachableException($"Could not find queue ARN for queue {_queueUrl}");

            bool exists = false;
            ListSubscriptionsByTopicResponse response;
            do
            {
                response = snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest {TopicArn = ChannelTopicArn}).GetAwaiter().GetResult();
                exists = response.Subscriptions.Any(sub => (sub.Protocol.ToLower() == "sqs") && (sub.Endpoint == queueArn));
            } while (!exists && response.NextToken != null);

            return exists;
        }

        public void DeleteQueue()
        {
            if (_subscription == null)
                return;

            using var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region);
            //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
            (bool exists, string name) queueExists = QueueExists(sqsClient, _subscription.ChannelName.ToValidSQSQueueName());

            if (queueExists.exists)
            {
                try
                {
                    sqsClient.DeleteQueueAsync(queueExists.name).Wait();
                }
                catch (Exception)
                {
                    //don't break on an exception here, if we can't delete, just exit
                    s_logger.LogError("Could not delete queue {ChannelName}", queueExists.name);
                }
            }
        }

        public void DeleteTopic()
        {
            if (_subscription == null)
                return;

            using var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region);
            (bool exists, string topicArn) = new ValidateTopicByArn(snsClient).Validate(ChannelTopicArn);
            if (exists)
            {
                try
                {
                    UnsubscribeFromTopic(snsClient);

                    DeleteTopic(snsClient);
                }
                catch (Exception)
                {
                    //don't break on an exception here, if we can't delete, just exit
                    s_logger.LogError("Could not delete topic {TopicResourceName}", ChannelTopicArn);
                }
            }
        }

        private void DeleteTopic(AmazonSimpleNotificationServiceClient snsClient)
        {
            snsClient.DeleteTopicAsync(ChannelTopicArn).GetAwaiter().GetResult();
        }


        private string GetQueueARNForChannel(AmazonSQSClient sqsClient)
        {
            var result = sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest {QueueUrl = _queueUrl, AttributeNames = new List<string> {"QueueArn"}}
            ).GetAwaiter().GetResult();

            if (result.HttpStatusCode == HttpStatusCode.OK)
            {
                return result.QueueARN;
            }

            return null;
        }

        private void UnsubscribeFromTopic(AmazonSimpleNotificationServiceClient snsClient)
        {
            ListSubscriptionsByTopicResponse response;
            do
            {
                response = snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest {TopicArn = ChannelTopicArn}).GetAwaiter().GetResult();
                foreach (var sub in response.Subscriptions)
                {
                    var unsubscribe = snsClient.UnsubscribeAsync(new UnsubscribeRequest {SubscriptionArn = sub.SubscriptionArn}).GetAwaiter().GetResult();
                    if (unsubscribe.HttpStatusCode != HttpStatusCode.OK)
                    {
                        s_logger.LogError("Error unsubscribing from {TopicResourceName} for sub {ChannelResourceName}", ChannelTopicArn, sub.SubscriptionArn);
                    }
                }
            } while (response.NextToken != null);
        }
    }
}
