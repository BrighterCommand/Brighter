using System.Linq;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    public class AWSQueueTools
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly string _queueName;
        private readonly string _topic;

        public AWSQueueTools(AWSMessagingGatewayConnection connection, string queueName)
        {
            _connection = connection;
            _queueName = queueName;
            _topic = queueName;

            using (var sqsClient = new AmazonSQSClient(_connection.Credentials))
            {
                if (!QueueExists(sqsClient, _queueName))
                {
                    CreateQueue(sqsClient);
                }
            }
        }

        public Topic CheckSnsTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient(_connection.Credentials))
            {
                return client.ListTopicsAsync().Result.Topics.SingleOrDefault(topic => topic.TopicArn == topicName);
            }
        }
        
        private void CreateQueue(AmazonSQSClient sqsClient)
        {
            var request = new CreateQueueRequest(_queueName);
            var response = sqsClient.CreateQueueAsync(request).Result;
            var queueUrl = response.QueueUrl;
            if (!string.IsNullOrEmpty(queueUrl))
            {
                using (var snsClient = new AmazonSimpleNotificationServiceClient(_connection.Credentials, _connection.Region))
                {
                    var subscription = snsClient.SubscribeQueueAsync(_topic, sqsClient, queueUrl).Result;
                }
            }
        }

        public void DeleteTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient(_connection.Credentials))
            {
                var topic = client.ListTopicsAsync().Result.Topics.SingleOrDefault(t => t.TopicArn == topicName);
                client.DeleteTopicAsync(topic.TopicArn).Wait();
            }
        }

        public void DeleteQueue()
        {
            using (var sqsClient = new AmazonSQSClient(_connection.Credentials))
            {
                if (QueueExists(sqsClient, _queueName))
                {
                    var urlResponse = sqsClient.GetQueueUrlAsync(_queueName).Result;
                    var deleteRequest = new DeleteQueueRequest(urlResponse.QueueUrl);
                    sqsClient.DeleteQueueAsync(deleteRequest).Wait();
                }
            }
        }
        
        private bool QueueExists(AmazonSQSClient sqsClient, string queueName)
        {
            var response = sqsClient.GetQueueUrlAsync(queueName).Result;
            return !string.IsNullOrWhiteSpace(response.QueueUrl);
        }

        public void DeleteMessage(string receiptHandle)
        {
            using (var client = new AmazonSQSClient(_connection.Credentials))
            {
                var urlResponse = client.GetQueueUrlAsync(_queueName).Result;
                client.DeleteMessageAsync(urlResponse.QueueUrl, receiptHandle).Wait();
            }
        }

   }
}
