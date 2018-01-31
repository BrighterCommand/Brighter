using System;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlInputChannelFactory : IAmAChannelFactory
    {
        private static readonly Lazy<ILog> Logger = new Lazy<ILog>(LogProvider.For<MsSqlInputChannelFactory>);
        private readonly MsSqlMessageConsumerFactory _msSqlMessageConsumerFactory;

        public MsSqlInputChannelFactory(MsSqlMessageConsumerFactory msSqlMessageConsumerFactory)
        {
            _msSqlMessageConsumerFactory = msSqlMessageConsumerFactory ??
                                           throw new ArgumentNullException(nameof(msSqlMessageConsumerFactory));
        }

        public IAmAChannel CreateInputChannel(string channelName, string topic, bool isDurable = false,
            ushort preFetchSize = 1,
            bool highAvailability = false)
        {
            Logger.Value.Debug($"MsSqlInputChannelFactory: create input channel {channelName} for topic {topic}");
            return new Channel(channelName,
                _msSqlMessageConsumerFactory.Create(channelName, topic, isDurable, preFetchSize, highAvailability));
        }
    }
}