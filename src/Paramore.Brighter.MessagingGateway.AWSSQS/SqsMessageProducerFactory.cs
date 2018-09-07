using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly AWSMessagingGatewayConnection _connection;

        public SqsMessageProducerFactory(AWSMessagingGatewayConnection connection)
        {
            _connection = connection;
        }

        public IAmAMessageProducer Create()
        {
            return new SqsMessageProducer(_connection);
        }
    }
}
