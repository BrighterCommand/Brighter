namespace Paramore.Brighter.MessagingGateway.RMQ
{
    public class RmqMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly RmqMessagingGatewayConnection _connection;
        private readonly RmqPublication _publication;

        public RmqMessageProducerFactory(
            RmqMessagingGatewayConnection connection,
            RmqPublication publication = null)
        {
            _connection = connection;
            _publication = publication ?? new RmqPublication{MakeChannels = OnMissingChannel.Create};
        }
        
        public IAmAMessageProducer Create()
        {
            return new RmqMessageProducer(_connection, _publication);
        }
    }
}
