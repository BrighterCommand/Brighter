using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public partial class MsSqlMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly ILogger _logger;
        private readonly ILoggerFactory? _loggerFactory;
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
        /// <param name="loggerFactory">The optional <see cref="ILoggerFactory"/> used to create loggers</param>
        public MsSqlMessageConsumerFactory(RelationalDatabaseConfiguration msSqlConfiguration, IAmAMessageScheduler? scheduler = null, ILoggerFactory? loggerFactory = null)
        {
            _msSqlConfiguration = msSqlConfiguration ?? throw new ArgumentNullException(nameof(msSqlConfiguration));
            _scheduler = scheduler;
            _loggerFactory = loggerFactory;
            _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<MsSqlMessageConsumerFactory>();
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync</returns>
         public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreate(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, _scheduler, deadLetterRoutingKey, invalidMessageRoutingKey, _loggerFactory);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            if (subscription.ChannelName is null) throw new ConfigurationException(nameof(subscription.ChannelName));

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreateAsync(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!, _scheduler, deadLetterRoutingKey, invalidMessageRoutingKey, _loggerFactory);
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

