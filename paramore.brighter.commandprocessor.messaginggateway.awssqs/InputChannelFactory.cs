namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public class InputChannelFactory : IAmAChannelFactory
    {
        private readonly SqsMessageConsumerFactory _messageConsumerFactory;
        private readonly SqsMessageProducerFactory _messageProducerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputChannelFactory"/> class.
        /// </summary>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        /// <param name="messageProducerFactory">The messageProducerFactory.</param>
        public InputChannelFactory(SqsMessageConsumerFactory messageConsumerFactory, SqsMessageProducerFactory messageProducerFactory)
        {
            _messageConsumerFactory = messageConsumerFactory;
            _messageProducerFactory = messageProducerFactory;
        }

        ///  <summary>
        ///  Creates the input channel.
        ///  </summary>
        ///  <param name="channelName">Name of the channel.</param>
        ///  <param name="routingKey">The routing key.</param>
        ///  <param name="isDurable"></param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannel CreateInputChannel(string channelName, string routingKey, bool isDurable)
        {
            return new Channel(channelName, _messageConsumerFactory.Create(channelName, routingKey, isDurable));
        }
    }
}
