using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public partial class MsSqlMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly RelationalDatabaseConfiguration _msSqlConfiguration;
        private IAmAMessageScheduler? _scheduler;

        /// <summary>
        /// Gets the configuration for the MsSql database, so that a channel factory built on this
        /// one can provision the queue store the consumers will read from. Deliberately internal:
        /// the only caller is <see cref="ChannelFactory"/> in this assembly, and this object carries
        /// the connection string, so there is no reason to widen a shipped package's public surface
        /// or to offer a second public route to it.
        /// </summary>
        internal RelationalDatabaseConfiguration Configuration => _msSqlConfiguration;

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
        /// <param name="loggerFactory">The optional <see cref="ILoggerFactory"/> used to create loggers</param>
        public MsSqlMessageConsumerFactory(RelationalDatabaseConfiguration msSqlConfiguration, ILoggerFactory loggerFactory, IAmAMessageScheduler? scheduler = null)
        {
            _msSqlConfiguration = msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            _scheduler = scheduler;
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateBrighterLogger<MsSqlMessageConsumerFactory>();
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
         public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            if (subscription.ChannelName is null)
                throw new ConfigurationException(nameof(subscription.ChannelName));

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreate(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, _loggerFactory, _scheduler, deadLetterRoutingKey, invalidMessageRoutingKey);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            if (subscription.ChannelName is null)
                throw new ConfigurationException(nameof(subscription.ChannelName));

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreateAsync(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, _loggerFactory, _scheduler, deadLetterRoutingKey, invalidMessageRoutingKey);
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

