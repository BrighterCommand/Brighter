using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageConsumerFactory>();
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;

        public MsSqlMessageConsumerFactory(RelationalDatabaseConfiguration msSqlConfiguration)
        {
            _msSqlConfiguration = msSqlConfiguration ??
                                                  throw new ArgumentNullException(
                                                      nameof(msSqlConfiguration));
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer</returns>
         public IAmAMessageConsumer Create(Subscription subscription)
        {
            if (subscription.ChannelName == null) throw new ArgumentNullException(nameof(subscription.ChannelName));
            s_logger.LogDebug("MsSqlMessageConsumerFactory: create consumer for topic {ChannelName}", subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName);
        }
    }
}
