namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class InputChannelFactory : IAmAChannelFactory
    {
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputChannelFactory"/> class.
        /// </summary>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public InputChannelFactory(SqsMessageConsumerFactory messageConsumerFactory)
        {
            _messageConsumerFactory = messageConsumerFactory;
        }

        ///  <summary>
        ///  Creates the input channel.
        ///  </summary>
        ///  <param name="channelName">Name of the channel.</param>
        ///  <param name="routingKey">The routing key.</param>
        ///  <param name="isDurable"></param>
        /// <param name="preFetchSize"></param>
        /// <param name="highAvailability"></param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannel CreateInputChannel(string channelName, string routingKey, bool isDurable = false, ushort preFetchSize = 1, bool highAvailability = false)
        {
            return new Channel(channelName, _messageConsumerFactory.Create(channelName, routingKey, isDurable, preFetchSize, highAvailability));
        }
    }
}
