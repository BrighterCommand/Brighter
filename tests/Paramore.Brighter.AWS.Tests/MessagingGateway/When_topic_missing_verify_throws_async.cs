using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    public class AWSValidateMissingTopicTestsAsync 
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly RoutingKey _routingKey;

        public AWSValidateMissingTopicTestsAsync()
        {
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _routingKey = new RoutingKey(topicName);

            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            // Because we don't use channel factory to create the infrastructure - it won't exist
        }

        [Fact]
        public async Task When_topic_missing_verify_throws_async()
        {
            // arrange
            var producer = new SqsMessageProducer(_awsConnection,
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Validate
                });

            // act & assert
            await Assert.ThrowsAsync<BrokerUnreachableException>(async () => 
                await producer.SendAsync(new Message(
                    new MessageHeader("", _routingKey, MessageType.MT_EVENT, type: "plain/text"),
                    new MessageBody("Test"))));
        }
    }
}
