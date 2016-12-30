using Amazon.Runtime;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
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