using System.Linq;

using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace paramore.commandprocessor.tests.MessagingGateway.awssqs
{
    public class TestAWSQueueListener
    {
        private string _queueUrl;
        private AmazonSQSClient _client;

        public TestAWSQueueListener(string queueUrl = "")
        {
            _queueUrl = queueUrl;
            _client = new AmazonSQSClient();
        }

        public Message Listen()
        {
            var response = _client.ReceiveMessage(_queueUrl);
            if (!response.Messages.Any()) return null;

            return response.Messages.First();
        }

        public Topic CheckSnsTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                return client.FindTopic(topicName);
            }

        }

        public void Purge(string queueUrl)
        {
            _client.PurgeQueue(_queueUrl);
        }

        public void DeleteMessage(string receiptHandle)
        {
            _client.DeleteMessage(_queueUrl, receiptHandle);
        }

        public void DeleteTopic(string topicName)
        {
            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var topic = client.FindTopic(topicName);
                client.DeleteTopic(topic.TopicArn);
            }
        }
    }
}