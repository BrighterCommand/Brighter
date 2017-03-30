using System.Linq;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    public class TestAWSQueueListener
    {
        private readonly AWSCredentials _credentials;
        private readonly string _queueUrl;
        private readonly AmazonSQSClient _client;

        public TestAWSQueueListener(AWSCredentials credentials, string queueUrl = "")
        {
            _credentials = credentials;
            _queueUrl = queueUrl;
            _client = new AmazonSQSClient(credentials);
        }

        public Amazon.SQS.Model.Message Listen()
        {
            var response = _client.ReceiveMessageAsync(_queueUrl).Result;
            if (!response.Messages.Any()) return null;

            return response.Messages.First();
        }

        public Topic CheckSnsTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient(_credentials))
            {
                return client.ListTopicsAsync().Result.Topics.SingleOrDefault(topic => topic.TopicArn == topicName);
            }
        }

        public void Purge(string queueUrl)
        {
            _client.PurgeQueueAsync(_queueUrl).Wait();
        }

        public void DeleteMessage(string receiptHandle)
        {
            _client.DeleteMessageAsync(_queueUrl, receiptHandle).Wait();
        }

        public void DeleteTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient(_credentials))
            {
                var topic = client.ListTopicsAsync().Result.Topics.SingleOrDefault(t => t.TopicArn == topicName);
                client.DeleteTopicAsync(topic.TopicArn).Wait();
            }
        }
    }
}