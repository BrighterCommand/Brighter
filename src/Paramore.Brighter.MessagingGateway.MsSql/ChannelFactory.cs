using System;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class ChannelFactory : IAmAChannelFactory
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ChannelFactory>();
        private readonly MsSqlMessageConsumerFactory _msSqlMessageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlMessageConsumerFactory"/> class.
        /// </summary>
        /// <param name="msSqlMessageConsumerFactory"></param>
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
            MsSqlSubscription rmqSubscription = subscription as MsSqlSubscription;  
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an MsSqlSubscription or MsSqlSubscription<T> as a parameter");
            
            s_logger.LogDebug("MsSqlInputChannelFactory: create input channel {ChannelName} for topic {Topic}", subscription.ChannelName, subscription.RoutingKey);
            return new Channel(
                subscription.ChannelName,
                _msSqlMessageConsumerFactory.Create(subscription),
                subscription.BufferSize);
        }
    }
}
