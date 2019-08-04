using System;
using System.Linq;
using System.Threading;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class ChannelFactory : IAmAChannelFactory
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<ChannelFactory>);
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="awsConnection">The details of the connection to AWS</param>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public ChannelFactory(
            AWSMessagingGatewayConnection awsConnection,
            SqsMessageConsumerFactory messageConsumerFactory)
        {
            _awsConnection = awsConnection;
            _messageConsumerFactory = messageConsumerFactory;
        }

        ///  <summary>
        ///  Creates the input channel.
        ///  With SQS we can ensure that queues exist ahead of creating the consumer, as there is no non-durable queue model
        ///  to create ephemeral queues, nor are there non-mirrored queues (on a single node in the cluster) where nodes
        ///  failing mean we want to create anew as we recreate. So the input factory creates the queue 
        ///  </summary>
        /// <param name="connection">The connection parameter so create the channel with</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannel CreateChannel(Connection connection)
        {
            EnsureQueue(connection);
            return new Channel(
                connection.ChannelName.ToValidSQSQueueName(), 
                _messageConsumerFactory.Create(connection), 
                connection.BufferSize
                );
        }

        private void EnsureQueue(Connection connection)
        {
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
                (bool exists, string name) queueExists = QueueExists(sqsClient, connection.ChannelName.ToValidSQSQueueName());
                if (!queueExists.exists)
                {
                    try
                    {
                        var request = new CreateQueueRequest(connection.ChannelName.ToValidSQSQueueName())
                        {
                            Attributes =
                            {
                                {"VisibilityTimeout", connection.VisibilityTimeout.ToString()},
                                {"ReceiveMessageWaitTimeSeconds",ToSecondsAsString(connection.TimeoutInMiliseconds) }
                            }
                        };
                        var response = sqsClient.CreateQueueAsync(request).Result;
                        var queueUrl = response.QueueUrl;
                        if (!string.IsNullOrEmpty(queueUrl))
                        {
                            //topic might not exist
                            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
                            {
                                var exists = snsClient.ListTopicsAsync().Result.Topics.SingleOrDefault(topic => topic.TopicArn == connection.RoutingKey);
                                if (exists == null)
                                {
                                    var createTopic = snsClient.CreateTopicAsync(new CreateTopicRequest(connection.RoutingKey.ToValidSNSTopicName())).Result;
                                    if (!string.IsNullOrEmpty(createTopic.TopicArn))
                                    {
                                        var subscription = snsClient.SubscribeQueueAsync(createTopic.TopicArn, sqsClient, queueUrl).Result;
                                        //We need to support raw messages to allow the use of message attributes
                                        snsClient.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest(subscription, "RawMessageDelivery", "true")).Wait();
                                    }
                                }
                            }
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
                                var error = $"Could not create queue {connection.ChannelName.ToValidSQSQueueName()} because {ae.Message} waiting 60s to retry";
                                _logger.Value.Error(error);
                                Thread.Sleep(TimeSpan.FromSeconds(30));
                                throw new ChannelFailureException(error, ae);
                            }

                            return false;
                        });
                    }
                }
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

                    //we didn't expect this, so rethrow
                    return false;
                });
            }

            return (exists, queueUrl);
        }

        public void DeleteQueue(Connection connection)
        {
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
                (bool exists, string name) queueExists = QueueExists(sqsClient, connection.ChannelName.ToValidSQSQueueName());
                if (!queueExists.exists)
                {
                    sqsClient.DeleteQueueAsync(queueExists.name).Wait();
                }

            }
        }

        public void DeleteTopic(Connection connection)
        {
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                //TODO: could be a seperate method
                var exists = snsClient.ListTopicsAsync().Result.Topics .SingleOrDefault(topic => topic.TopicArn == connection.RoutingKey);
                if (exists != null)
                {
                    snsClient.DeleteTopicAsync(connection.RoutingKey).Wait();
                }
            }
        }
    }
}
