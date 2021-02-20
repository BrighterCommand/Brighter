using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class ChannelFactory : IAmAChannelFactory
    {
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<ChannelFactory>);
        private readonly MsSqlMessageConsumerFactory _msSqlMessageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaInputChannelFactory"/> class.
        /// </summary>
        /// <param name="kafkaMessageConsumerFactory">The messageConsumerFactory.</param>
        public ChannelFactory(MsSqlMessageConsumerFactory msSqlMessageConsumerFactory)
        {
            _msSqlMessageConsumerFactory = msSqlMessageConsumerFactory ??
                                           throw new ArgumentNullException(nameof(msSqlMessageConsumerFactory));
        }

        /// <summary>
        /// Creates the input channel
        /// </summary>
        /// <param name="subscription">The subscription parameters with which to create the channel</param>
        /// <returns></returns>
        public IAmAChannel CreateChannel(Subscription subscription)
        {
            Logger.Value.Debug($"MsSqlInputChannelFactory: create input channel {subscription.ChannelName} for topic {subscription.RoutingKey}");
            return new Channel(
                subscription.ChannelName,
                _msSqlMessageConsumerFactory.Create(subscription),
                subscription.BufferSize);
        }
    }
}
