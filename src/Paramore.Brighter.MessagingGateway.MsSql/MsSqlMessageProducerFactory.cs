using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly MsSqlMessagingGatewayConfiguration _msSqlMessagingGatewayConfiguration;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducerFactory>();
        private Publication _publication; //-- placeholder for future use

        public MsSqlMessageProducerFactory(
            MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration,
            Publication publication = null)
        {
            _msSqlMessagingGatewayConfiguration = 
                msSqlMessagingGatewayConfiguration ?? throw new ArgumentNullException(nameof(msSqlMessagingGatewayConfiguration));
            _publication = publication ?? new Publication() {MakeChannels = OnMissingChannel.Create};
        }

        public IAmAMessageProducer Create()
        {
            s_logger.LogDebug("MsSqlMessageProducerFactory: create producer");
            return new MsSqlMessageProducer(_msSqlMessagingGatewayConfiguration, _publication);
        }
    }
}
