using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <summary>
    /// There is a default implementation to build Kafka message before sending to a Kafka topic.
    /// This interface allow a custom implementation of building the message header.
    /// </summary>
    public interface IKafkaMessageHeaderBuilder
    {
        /// <summary>
        /// Method to build the message header.
        /// </summary>
        /// <param name="message">The Brighter Message</param>
        /// <returns>The Headers object</returns>
        Headers Build(Message message);
    }
}
