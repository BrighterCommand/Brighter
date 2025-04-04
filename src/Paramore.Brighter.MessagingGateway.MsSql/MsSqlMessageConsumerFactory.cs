using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public partial class MsSqlMessageConsumerFactory(RelationalDatabaseConfiguration msSqlConfiguration) : IAmAMessageConsumerFactory
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageConsumerFactory>();
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration = msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
         public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));
            
            Log.MsSqlMessageConsumerFactoryCreate(s_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));
            
            Log.MsSqlMessageConsumerFactoryCreateAsync(s_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "MsSqlMessageConsumerFactory: create consumer for topic {ChannelName}")]
            public static partial void MsSqlMessageConsumerFactoryCreate(ILogger logger, string? channelName);

            [LoggerMessage(LogLevel.Debug, "MsSqlMessageConsumerFactory: create consumer for topic {ChannelName}")]
            public static partial void MsSqlMessageConsumerFactoryCreateAsync(ILogger logger, string? channelName);
        }
    }
}

