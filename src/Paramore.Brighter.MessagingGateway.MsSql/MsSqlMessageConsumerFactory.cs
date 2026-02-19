using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public partial class MsSqlMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageConsumerFactory>();
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;
        private IAmAMessageScheduler? _scheduler;

        /// <summary>
        /// Gets or sets the message scheduler for delayed requeue support.
        /// Can be set after construction to allow channel factories to forward the scheduler from DI.
        /// </summary>
        public IAmAMessageScheduler? Scheduler
        {
            get => _scheduler;
            set => _scheduler = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlMessageConsumerFactory"/> class.
        /// </summary>
        /// <param name="msSqlConfiguration">The configuration for connecting to the MsSql database</param>
        /// <param name="scheduler">The optional message scheduler for delayed requeue support</param>
        public MsSqlMessageConsumerFactory(RelationalDatabaseConfiguration msSqlConfiguration, IAmAMessageScheduler? scheduler = null)
        {
            _msSqlConfiguration = msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            _scheduler = scheduler;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
         public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));
            
            Log.MsSqlMessageConsumerFactoryCreate(s_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, scheduler: _scheduler);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));

            Log.MsSqlMessageConsumerFactoryCreateAsync(s_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, scheduler: _scheduler);
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

