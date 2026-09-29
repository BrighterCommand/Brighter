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
            ValidateSubscription(subscription, false);

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreate(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!,
                (subscription as MsSqlSubscription)?.IsIndependentSubscriber ?? false,
                loggerFactory: _loggerFactory, scheduler: _scheduler, deadLetterRoutingKey: deadLetterRoutingKey, invalidMessageRoutingKey: invalidMessageRoutingKey);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            ValidateSubscription(subscription, true);

            var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
            var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

            Log.MsSqlMessageConsumerFactoryCreateAsync(_logger, subscription.ChannelName);
            return new MsSqlMessageConsumer(_msSqlConfiguration, subscription.ChannelName!,
                (subscription as MsSqlSubscription)?.IsIndependentSubscriber ?? false,
                loggerFactory: _loggerFactory, scheduler: _scheduler, deadLetterRoutingKey: deadLetterRoutingKey, invalidMessageRoutingKey: invalidMessageRoutingKey);
        }

        internal void ValidateSubscription(Subscription subscription, bool async)
        {
            if (subscription.ChannelName is null)
                throw new ConfigurationException(nameof(subscription.ChannelName));
            if (subscription.RequeueDelay <= TimeSpan.Zero || subscription is not MsSqlSubscription { IsIndependentSubscriber: true })
                return;

            if (async ? _scheduler is not IAmAMessageRequeueSchedulerAsync : _scheduler is not IAmAMessageRequeueSchedulerSync)
                throw new ConfigurationException("Delayed MS SQL subscriber retries require a queue-targeted scheduler implementing " +
                    (async ? nameof(IAmAMessageRequeueSchedulerAsync) : nameof(IAmAMessageRequeueSchedulerSync)) + ".");
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
