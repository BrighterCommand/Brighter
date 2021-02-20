using System;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class AWSMessagingGateway
    {
        protected static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<ChannelFactory>);
        protected AWSMessagingGatewayConnection _awsConnection;
        protected string _channelTopicArn;

        public AWSMessagingGateway(AWSMessagingGatewayConnection awsConnection)
        {
            _awsConnection = awsConnection;
        }

        protected string EnsureTopic(SqsSubscription sqsSubscription)
        {
            return EnsureTopic(sqsSubscription.RoutingKey, sqsSubscription.MakeChannels);
        }

        protected string EnsureTopic(RoutingKey topic, OnMissingChannel makeTopic)
        {
            if (makeTopic == OnMissingChannel.Assume)
                _channelTopicArn = topic.Value;
            else if (makeTopic == OnMissingChannel.Validate)
                ValidateTopic(topic);
            else if (makeTopic == OnMissingChannel.Create) CreateTopic(topic);
            return _channelTopicArn;
        }

        private void CreateTopic(RoutingKey topicName)
        {
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                var createTopic = snsClient.CreateTopicAsync(new CreateTopicRequest(topicName)).Result;
                if (!string.IsNullOrEmpty(createTopic.TopicArn))
                    _channelTopicArn = createTopic.TopicArn;
                else
                    throw new InvalidOperationException($"Could not create Topic topic: {topicName} on {_awsConnection.Region}");
            }
        }

        private void ValidateTopic(RoutingKey topic)
        {
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                var (success, arn) = FindTopicByName(topic.ToValidSNSTopicName(), snsClient);
                if (success)
                    _channelTopicArn = arn;
                else
                    throw new BrokerUnreachableException(
                        $"Topic validation error: could not find topic {topic.ToValidSNSTopicName()}. Did you want Brighter to create infrastructure?");
            }
        }

        private static (bool success, string topicArn) FindTopicByName(RoutingKey topicName, AmazonSimpleNotificationServiceClient snsClient)
        {
            var topic = snsClient.FindTopicAsync(topicName.Value).GetAwaiter().GetResult();
            return (topic == null, topic.TopicArn);
        }
    }
}
