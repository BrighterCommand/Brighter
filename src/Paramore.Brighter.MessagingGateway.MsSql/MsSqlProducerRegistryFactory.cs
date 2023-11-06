using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlProducerRegistryFactory>();
        private readonly IEnumerable<Publication> _publications; //-- placeholder for future use

        public MsSqlProducerRegistryFactory(
            RelationalDatabaseConfiguration msSqlConfiguration,
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

            var producers = new Dictionary<string, IAmAMessageProducer>();

            foreach (var publication in _publications)
            {
                producers[publication.Topic] = new MsSqlMessageProducer(_msSqlConfiguration, publication);
            }

            return new ProducerRegistry(producers);
        }
    }
}
