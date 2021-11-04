using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly MsSqlConfiguration _msSqlConfiguration;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducerFactory>();
        private Publication _publication; //-- placeholder for future use

        public MsSqlMessageProducerFactory(
            MsSqlConfiguration msSqlConfiguration,
            Publication publication = null)
        {
            _msSqlConfiguration = 
                msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            if (string.IsNullOrEmpty(msSqlConfiguration.QueueStoreTable))
                throw new ArgumentNullException(nameof(msSqlConfiguration.QueueStoreTable));
            _publication = publication ?? new Publication() {MakeChannels = OnMissingChannel.Create};
        }

        public IAmAMessageProducerSync Create()
        {
            s_logger.LogDebug("MsSqlMessageProducerFactory: create producer");
            return new MsSqlMessageProducer(_msSqlConfiguration, _publication);
        }
    }
}
