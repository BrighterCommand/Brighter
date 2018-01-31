using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlMessageConsumerFactory>);
        private readonly MsSqlMessagingGatewayConfiguration _msSqlMessagingGatewayConfiguration;

        public MsSqlMessageConsumerFactory(MsSqlMessagingGatewayConfiguration msSqlMessagingGatewayConfiguration)
        {
            _msSqlMessagingGatewayConfiguration = msSqlMessagingGatewayConfiguration ??
                                                  throw new ArgumentNullException(
                                                      nameof(msSqlMessagingGatewayConfiguration));
        }

        public IAmAMessageConsumer Create(string channelName, string topic, bool isDurable, ushort preFetchSize,
            bool highAvailability)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            Logger.Value.Debug($"MsSqlMessageConsumerFactory: create consumer for topic {topic}");
            return new MsSqlMessageConsumer(_msSqlMessagingGatewayConfiguration, topic);
        }
    }
}