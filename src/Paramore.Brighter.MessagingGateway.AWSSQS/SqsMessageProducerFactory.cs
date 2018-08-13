using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly AWSCredentials _credentials;
        private readonly RegionEndpoint _regionEndpoint;

        public SqsMessageProducerFactory(AWSCredentials credentials)
        {
            _credentials = credentials;
        }

        public SqsMessageProducerFactory(AWSCredentials credentials, RegionEndpoint regionEndpoint) : this(credentials)
        {
            _regionEndpoint = regionEndpoint;
        }

        public IAmAMessageProducer Create()
        {
            return new SqsMessageProducer(_credentials, _regionEndpoint);
        }
    }
}
