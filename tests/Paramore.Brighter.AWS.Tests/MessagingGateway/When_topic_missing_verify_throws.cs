using System;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")] 
    public class AWSValidateMissingTopicTests 
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly RoutingKey _routingKey;

        public AWSValidateMissingTopicTests()
        {
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _routingKey = new RoutingKey(topicName);
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            //Because we don't use channel factory to create the infrastructure -it won't exist
       }

        [Fact]
        public void When_topic_missing_verify_throws()
        {
            //arrange
            var producer = new SqsMessageProducer(_awsConnection, 
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Validate
                });
            
            //act && assert
            Assert.Throws<BrokerUnreachableException>(() => producer.Send(new Message(
                new MessageHeader{Topic = _routingKey, ContentType = "plain/text"},
                new MessageBody("Test"))));
        }
   }
}
