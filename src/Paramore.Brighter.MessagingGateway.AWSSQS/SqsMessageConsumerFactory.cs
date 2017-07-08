using Amazon.Runtime;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Class SqsMessageConsumerFactory.
    /// </summary>
    public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly AWSCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// </summary>
        public SqsMessageConsumerFactory(AWSCredentials credentials) 
        {
            _credentials = credentials;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">if set to <c>true</c> [is durable].</param>
        /// <param name="preFetchSize">Number of items to read from the queue at once</param>
        /// <param name="highAvailability">Our are queues high-availablility</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable, ushort preFetchSize = 1, bool highAvailability = false)
        {
            return new SqsMessageConsumer(_credentials, channelName);
        }
    }
}