using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly AWSMessagingGatewayConnection _connection;
        private readonly SqsPublication _sqsPublication;

        public SqsMessageProducerFactory(
            AWSMessagingGatewayConnection connection
            ) : this(connection, new SqsPublication{MakeChannels = OnMissingChannel.Create })
        {
            
        }
        
        public SqsMessageProducerFactory(
            AWSMessagingGatewayConnection connection,
            SqsPublication sqsPublication)
        {
            _connection = connection;
            _sqsPublication = sqsPublication;
        }

        public IAmAMessageProducerSync Create()
        {
            return new SqsMessageProducer(_connection, _sqsPublication);
        }
    }
}
