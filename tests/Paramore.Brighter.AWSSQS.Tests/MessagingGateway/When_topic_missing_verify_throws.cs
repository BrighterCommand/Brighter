using System;
using System.Linq;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    public class AWSValidateMissingTopicTests 
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly RoutingKey _routingKey;

        public AWSValidateMissingTopicTests()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
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
            Assert.Throws<BrokerUnreachableException>(() => 
                new SqsMessageProducer(
                    _awsConnection, 
                    new SqsPublication
                    {
                        MakeChannels = OnMissingChannel.Validate, 
                        RoutingKey = _routingKey
                    }));
            
        }
   }
}
