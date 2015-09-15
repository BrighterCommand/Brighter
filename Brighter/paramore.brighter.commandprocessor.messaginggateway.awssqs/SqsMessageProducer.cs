using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class SqsMessageProducer : IAmAMessageProducer
    {
        private readonly ILog _logger;

        public SqsMessageProducer(ILog logger)
        {
            _logger = logger;
        }

        public void Send(Message message)
        {
            var messageString = JsonConvert.SerializeObject(message);
            _logger.DebugFormat("SQSMessageProducer: Publishing message with topic {0} and id {1} and message: {2}", message.Header.Topic, message.Id, messageString);
            
            using (var client = new AmazonSimpleNotificationServiceClient())
            {
                var topicArn = EnsureTopic(message.Header.Topic, client);

                var publishRequest = new PublishRequest(topicArn, messageString);
                client.Publish(publishRequest);
            }
        }

        private string EnsureTopic(string topicName, AmazonSimpleNotificationServiceClient client)
        {
            var topic = client.FindTopic(topicName);
            if (topic != null)
                return topic.TopicArn;

            _logger.DebugFormat("Topic with name {0} does not exist. Creating new topic", topicName);
            var topicResult = client.CreateTopic(topicName);
            return topicResult.HttpStatusCode == HttpStatusCode.OK ? topicResult.TopicArn : string.Empty;
        }

        public void Dispose()
        {
            
        }
    }
}