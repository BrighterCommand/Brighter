using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal class ValidateTopicByName : IValidateTopic
    {
        private readonly AmazonSimpleNotificationServiceClient _snsClient;

        public ValidateTopicByName(AWSCredentials credentials, RegionEndpoint region)
        {
            _snsClient = new AmazonSimpleNotificationServiceClient(credentials, region);
        }
        
        public ValidateTopicByName(AmazonSimpleNotificationServiceClient snsClient)
        {
            _snsClient = snsClient;
        }
        
        //Note that we assume here that topic names are globally unique, if not provide the topic ARN directly in the SNSAttributes of the subscription
        //This approach can have be rate throttled at scale. AWS limits to 30 ListTopics calls per second, so it you have a lot of clients starting
        //you may run into issues
        public (bool, string TopicArn) Validate(string topicName)
        {
            var topic = _snsClient.FindTopicAsync(topicName).GetAwaiter().GetResult();
            return (topic != null, topic?.TopicArn);
        }
    }
}
