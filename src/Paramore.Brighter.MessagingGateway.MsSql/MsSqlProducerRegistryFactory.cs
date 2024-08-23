using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly MsSqlConfiguration _msSqlConfiguration;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlProducerRegistryFactory>();
        private readonly IEnumerable<Publication> _publications; //-- placeholder for future use

        public MsSqlProducerRegistryFactory(
            MsSqlConfiguration msSqlConfiguration,
            IEnumerable<Publication> publications)
        {
            _msSqlConfiguration = 
                msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            if (string.IsNullOrEmpty(msSqlConfiguration.QueueStoreTable))
                throw new ArgumentNullException(nameof(msSqlConfiguration.QueueStoreTable));
            _publications = publications;
        }

        public IAmAProducerRegistry Create()
        {
            s_logger.LogDebug("MsSqlMessageProducerFactory: create producer");

            var producerFactory = new MsSqlMessageProducerFactory(_msSqlConfiguration, _publications);

            return new ProducerRegistry(producerFactory.Create());
        }
    }
}
