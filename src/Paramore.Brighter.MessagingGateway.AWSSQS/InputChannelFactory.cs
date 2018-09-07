using System;
using System.Diagnostics.SymbolStore;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class InputChannelFactory : IAmAChannelFactory
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<InputChannelFactory>);
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputChannelFactory"/> class.
        /// </summary>
        /// <param name="awsConnection">The details of the connection to AWS</param>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public InputChannelFactory(
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
        public IAmAChannel CreateInputChannel(Connection connection)
        {
            EnsureQueue(connection);
            return new Channel(connection.ChannelName, _messageConsumerFactory.Create(connection));
        }

        private void EnsureQueue(Connection connection)
        {
            using (var sqsClient = new AmazonSQSClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                //Does the queue exist - this is an HTTP call, we should cache the results for a period of time
                if (!QueueExists(sqsClient, connection.ChannelName))
                {
                    try
                    {
                        var request = new CreateQueueRequest(connection.ChannelName)
                        {
                            Attributes =
                            {
                                {"VisibilityTimeout", connection.VisibilityTimeout.ToString()},
                                {"ReceiveMessageWaitTimeoutSeconds",ToSecondsAsString(connection.TimeoutInMiliseconds) }
                            }
                        };
                        var response = sqsClient.CreateQueueAsync(request).Result;
                        var queueUrl = response.QueueUrl;
                        if (!string.IsNullOrEmpty(queueUrl))
                        {
                            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
                            {
                                var subscription = snsClient.SubscribeQueueAsync(connection.RoutingKey, sqsClient, queueUrl).Result;
                            }
                        }
     
                    }
                    catch (AmazonSQSException e)
                    {
                        //We need some retry semantics here
                        var error = $"Could not create queue {connection.ChannelName} because {e.Message}";
                        _logger.Value.Error(error);
                        throw new ChannelFailureException(error, e);
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

        private bool QueueExists(AmazonSQSClient client, string channelName)
        {
            bool exists = false;
            try
            {
                var response = client.GetQueueUrlAsync(channelName).Result;
                //If the queue does not exist yet then
                if (!string.IsNullOrWhiteSpace(response.QueueUrl))
                {
                    exists = true;
                }
            }
            catch (QueueDoesNotExistException)
            {
                exists = false;
            }

            return exists;
        }

        public void DeleteQueue()
        {
            throw new NotImplementedException();
        }

        public void DeleteTopic()
        {
            throw new NotImplementedException();
        }
    }
}
