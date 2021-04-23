using System;
using System.Collections.Generic;
using System.Net;
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

        protected string EnsureTopic(RoutingKey topic, SnsAttributes attributes, OnMissingChannel makeTopic)
        {
            //on validate or assume, turn a routing key into a topicARN
            if ((makeTopic == OnMissingChannel.Assume) || (makeTopic == OnMissingChannel.Validate)) 
                ValidateTopic(topic, attributes, makeTopic);
            else if (makeTopic == OnMissingChannel.Create) CreateTopic(topic, attributes);
            return _channelTopicArn;
        }

        private void CreateTopic(RoutingKey topicName, SnsAttributes snsAttributes)
        {
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                var attributes = new Dictionary<string, string>();
                if (snsAttributes != null)
                {
                    if (!string.IsNullOrEmpty(snsAttributes.DeliveryPolicy)) attributes.Add("DeliveryPolicy", snsAttributes.DeliveryPolicy);
                    if (!string.IsNullOrEmpty(snsAttributes.Policy)) attributes.Add("Policy", snsAttributes.Policy);
                }

                var createTopicRequest = new CreateTopicRequest(topicName)
                {
                    Attributes = attributes,
                    Tags = new List<Tag> {new Tag {Key = "Source", Value = "Brighter"}}
                };
                
                var createTopic = snsClient.CreateTopicAsync(createTopicRequest).Result;
                
                if (!string.IsNullOrEmpty(createTopic.TopicArn))
                    _channelTopicArn = createTopic.TopicArn;
                else
                    throw new InvalidOperationException($"Could not create Topic topic: {topicName} on {_awsConnection.Region}");
            }
        }

        private void ValidateTopic(RoutingKey topic, SnsAttributes attributes, OnMissingChannel onMissingChannel)
        {
            if ((attributes != null) && (!string.IsNullOrEmpty(attributes.TopicARN)))
            {
                if (onMissingChannel == OnMissingChannel.Assume)
                {
                    _channelTopicArn = attributes.TopicARN;
                    return;
                }
                else
                {
                    ValidateTopicByArn(attributes.TopicARN);
                }
            }

            ValidateTopicByName(topic);
        }

        private bool ValidateTopicByArn(string topicArn)
        {
            using (var snsClient = new AmazonSimpleNotificationServiceClient(_awsConnection.Credentials, _awsConnection.Region))
            {
                var response = snsClient.GetTopicAttributesAsync(new GetTopicAttributesRequest(topicArn))
                    .GetAwaiter()
                    .GetResult();
                return ((response.HttpStatusCode == HttpStatusCode.OK) && (response.Attributes["TopicArn"] == topicArn));
            }
        }

        private void ValidateTopicByName(RoutingKey topic)
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

        //Note that we assume here that topic names are globally unique, if not provide the topic ARN directly in the SNSAttributes of the subscription
        private static (bool success, string topicArn) FindTopicByName(RoutingKey topicName, AmazonSimpleNotificationServiceClient snsClient)
        {
            var topic = snsClient.FindTopicAsync(topicName.Value).GetAwaiter().GetResult();
            return (topic != null, topic?.TopicArn);
        }
    }
}
