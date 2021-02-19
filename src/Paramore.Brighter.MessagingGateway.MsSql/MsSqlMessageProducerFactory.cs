using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly MsSqlMessagingGatewayConfiguration _msSqlMessagingGatewayConfiguration;
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageProducerFactory>);
        private ProducerConnection _producerConnection; //-- placeholder for future use

        public MsSqlMessageProducerFactory(
            MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration,
            ProducerConnection producerConnection = null)
        {
            _msSqlMessagingGatewayConfiguration = 
                msSqlMessagingGatewayConfiguration ?? throw new ArgumentNullException(nameof(msSqlMessagingGatewayConfiguration));
            _producerConnection = producerConnection ?? new ProducerConnection() {MakeChannels = OnMissingChannel.Create};
        }

        public IAmAMessageProducer Create()
        {
            Logger.Value.Debug($"MsSqlMessageProducerFactory: create producer");
            return new MsSqlMessageProducer(_msSqlMessagingGatewayConfiguration, _producerConnection);
        }
    }
}
