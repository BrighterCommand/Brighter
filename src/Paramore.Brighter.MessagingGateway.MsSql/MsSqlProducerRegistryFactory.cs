using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

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

        /// <summary>
        /// Creates a message producer registry.
        /// </summary>
        /// <returns>A registry of middleware clients by topic, for sending messages to the middleware</returns>
        public IAmAProducerRegistry Create()
        {
            s_logger.LogDebug("MsSqlMessageProducerFactory: create producer");

            var producerFactory = new MsSqlMessageProducerFactory(_msSqlConfiguration, _publications);

            return new ProducerRegistry(producerFactory.Create());
        }

        /// <summary>
        /// Creates a message producer registry.
        /// </summary>
        /// <remarks>
        /// Mainly useful where the producer creation is asynchronous, such as when connecting to a remote service to create or validate infrastructure
        /// </remarks>
        /// <param name="ct">A cancellation token to cancel the operation</param>
        /// <returns>A registry of middleware clients by topic, for sending messages to the middleware</returns>
        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
           return Task.FromResult(Create()); 
        }
    }
}
