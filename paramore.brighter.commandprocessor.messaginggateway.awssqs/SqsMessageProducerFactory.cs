using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly AWSCredentials _credentials;
        public SqsMessageProducerFactory(AWSCredentials credentials)
        {
            _credentials = credentials;
        }

        public IAmAMessageProducer Create()
        {
            return new SqsMessageProducer(_credentials);
        }
    }
}