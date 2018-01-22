using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly MsSqlMessagingGatewayConfiguration _msSqlMessagingGatewayConfiguration;
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageProducerFactory>);

        public MsSqlMessageProducerFactory(MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration)
        {
            _msSqlMessagingGatewayConfiguration = msSqlMessagingGatewayConfiguration ??
                                                  throw new ArgumentNullException(
                                                      nameof(msSqlMessagingGatewayConfiguration));
        }

        public IAmAMessageProducer Create()
        {
            Logger.Value.Debug($"MsSqlMessageProducerFactory: create producer");
            return new MsSqlMessageProducer(_msSqlMessagingGatewayConfiguration);
        }
    }
}