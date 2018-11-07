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

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="connection">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer</returns>
         public IAmAMessageConsumer Create(Connection connection)
        {
            if (connection.ChannelName == null) throw new ArgumentNullException(nameof(connection.ChannelName));
            Logger.Value.Debug($"MsSqlMessageConsumerFactory: create consumer for topic {connection.ChannelName}");
            return new MsSqlMessageConsumer(_msSqlMessagingGatewayConfiguration, connection.ChannelName);
        }
    }
}
