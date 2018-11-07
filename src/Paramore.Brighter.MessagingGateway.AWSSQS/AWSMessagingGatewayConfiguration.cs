using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class AWSMessagingGatewayConnection
    {
        public AWSMessagingGatewayConnection(AWSCredentials credentials, RegionEndpoint region)
        {
            Credentials = credentials;
            Region = region;
        }

        public AWSCredentials Credentials { get; }
        public RegionEndpoint Region { get; }
    }
}
