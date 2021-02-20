using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Amazon;
using Amazon.Runtime.Internal;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class ChannelFactory : AWSMessagingGateway, IAmAChannelFactory
    {
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;
        private Subscription _subscription;
        private string _queueUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="awsConnection">The details of the subscription to AWS</param>
        public ChannelFactory(
            AWSMessagingGatewayConnection awsConnection)
            :base(awsConnection)
        {
            _messageConsumerFactory = new SqsMessageConsumerFactory(awsConnection);
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
            SqsSubscription sqsSubscription = subscription as SqsSubscription;  
            if (sqsSubscription == null)
                throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");
            
            _subscription = null;
            EnsureTopic(sqsSubscription);
            EnsureQueue(sqsSubscription);
            _subscription = subscription;
            return new Channel(
                subscription.ChannelName.ToValidSQSQueueName(), 
                _messageConsumerFactory.Create(subscription), 
                subscription.BufferSize
                );
        }
        
        private void EnsureQueue(SqsSubscription subscription)
        {
            if (subscription.MakeChannels == OnMissingChannel.Assume)
                return;
            
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
                var queueName = subscription.ChannelName.ToValidSQSQueueName();
                var topicName = subscription.RoutingKey.ToValidSNSTopicName();

                (bool exists, _) = QueueExists(sqsClient, queueName);
                if (!exists)
                {
                    if (subscription.MakeChannels == OnMissingChannel.Create)
                    {
                        CreateQueue(sqsClient, subscription, queueName, topicName, _awsConnection.Region);
                    }
                    else if (subscription.MakeChannels == OnMissingChannel.Validate)
                    {
                        var message = $"Queue does not exist: {queueName} for {topicName} on {_awsConnection.Region}";
                        _logger.Value.Debug(message);
                        throw new QueueDoesNotExistException(message);
                    }
                }
                else
                {
                    _logger.Value.Debug($"Queue exists: {queueName} subscribed to {topicName} on {_awsConnection.Region}");
                }
            }
        }

        private void CreateQueue(AmazonSQSClient sqsClient, SqsSubscription subscription, ChannelName queueName, RoutingKey topicName, RegionEndpoint region)
        {
            _logger.Value.Debug($"Queue does not exist, creating queue: {queueName} subscribed to {topicName} on {_awsConnection.Region}");
            _queueUrl = "no queue defined";
            try
            {
                var request = new CreateQueueRequest(queueName)
                {
                    Attributes =
                    {
                        {"VisibilityTimeout", subscription.LockTimeout.ToString()},
                        {"ReceiveMessageWaitTimeSeconds", ToSecondsAsString(subscription.TimeoutInMiliseconds)}
                    },
                    Tags =
                    {
                        {"Source", "Brighter"},
                        {"Topic", $"{topicName.Value}"}
                    }
                };
                var response = sqsClient.CreateQueueAsync(request).Result;
                _queueUrl = response.QueueUrl;
                if (!string.IsNullOrEmpty(_queueUrl))
                {
                    _logger.Value.Debug($"Queue created: {_queueUrl}");
                    using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
                    {
                        CheckSubscription(subscription.MakeChannels, sqsClient, snsClient);
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Could not create queue: {queueName} subscribed to {_channelTopicArn} on {_awsConnection.Region}");
                }
            }
            catch (AggregateException ae)
            {
                //TODO: We need some retry semantics here
                //TODO: We need to flatten the ae and handle some of these with ae.Handle((x) => {})
                ae.Handle(ex =>
                {
                    if (ex is QueueDeletedRecentlyException)
                    {
                        //QueueDeletedRecentlyException - wait 30 seconds then retry
                        //Although timeout is 60s, we could be partway through that, so apply Copernican Principle 
                        //and assume we are halfway through
                        var error = $"Could not create queue {queueName} because {ae.Message} waiting 60s to retry";
                        _logger.Value.Error(error);
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        throw new ChannelFailureException(error, ae);
                    }

                    if (ex is AmazonSQSException || ex is HttpErrorResponseException)
                    {
                        var error = $"Could not create queue {_queueUrl} subscribed to topic {topicName} in region {region.DisplayName} because {ae.Message}";
                        _logger.Value.Error(error);
                        throw new InvalidOperationException(error, ex);
                    }

                    return false;
                });
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
            var subscription = snsClient.SubscribeQueueAsync(_channelTopicArn, sqsClient, _queueUrl).Result;
            if (!string.IsNullOrEmpty(subscription))
            {
                //We need to support raw messages to allow the use of message attributes
                var response = snsClient.SetSubscriptionAttributesAsync(
                        new SetSubscriptionAttributesRequest(
                            subscription, "RawMessageDelivery", "true")
                    )
                    .Result;
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"Unable to set subscription attribute for raw message delivery");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Could not subscribe to topic: {_channelTopicArn} from queue: {_queueUrl} in region {_awsConnection.Region}");
            }
        }

        private string ToSecondsAsString(int timeountInMilliseconds)
        {
            int timeOutInSeconds = 0;
            if (timeountInMilliseconds >= 1000)
                timeOutInSeconds = timeountInMilliseconds / 1000;
            else if (timeountInMilliseconds > 0)
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
                response = snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest {TopicArn = _channelTopicArn}).GetAwaiter().GetResult();
                exists = response.Subscriptions.Any(sub => (sub.Protocol.ToLower() == "sqs") && (sub.Endpoint == queueArn));
            } while (!exists && response.NextToken != null);

            return exists;
        }

        public void DeleteQueue()
        {
            if (_subscription == null)
                return;
            
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
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
                        _logger.Value.Error($"Could not delete queue {queueExists.name}");
                    }
                }

            }
        }

        public void DeleteTopic()
        {
            if (_subscription == null)
                return;
            
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                bool exists = FindTopicByArn(snsClient);
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
                        _logger.Value.Error($"Could not delete topic {_channelTopicArn}");
                    }
                }
            }
        }

        private void DeleteTopic(AmazonSimpleNotificationServiceClient snsClient)
        {
            snsClient.DeleteTopicAsync(_channelTopicArn).GetAwaiter().GetResult();
        }


        private bool FindTopicByArn(AmazonSimpleNotificationServiceClient snsClient)
        {
            bool exists = false;
            ListTopicsResponse response;
            do
            {
                response = snsClient.ListTopicsAsync().GetAwaiter().GetResult();
                exists = response.Topics.Any(topic => topic.TopicArn == _channelTopicArn);
            } while (!exists && response.NextToken != null);
            return exists;
        }
        
        private string GetQueueARNForChannel(AmazonSQSClient sqsClient)
        {
            var result = sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = _queueUrl,
                    AttributeNames = new List<string> {"QueueArn"}
                }
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
                response = snsClient.ListSubscriptionsByTopicAsync(new ListSubscriptionsByTopicRequest {TopicArn = _channelTopicArn}).GetAwaiter().GetResult();
                foreach (var sub in response.Subscriptions)
                {
                    var unsubscribe = snsClient.UnsubscribeAsync(new UnsubscribeRequest {SubscriptionArn = sub.SubscriptionArn}).GetAwaiter().GetResult();
                    if (unsubscribe.HttpStatusCode != HttpStatusCode.OK)
                    {
                        _logger.Value.Error($"Error unsubscribing from {_channelTopicArn} for sub {sub.SubscriptionArn}");
                    }
                }
            } while (response.NextToken != null);
        }

    }
}
