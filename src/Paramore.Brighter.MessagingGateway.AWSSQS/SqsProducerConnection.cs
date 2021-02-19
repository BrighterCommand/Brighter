namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsProducerConnection : ProducerConnection
    {
        /// <summary>
        /// Gets or sets the routing key or topic that this channel subscribes to on the broker.
        /// Either the topic name if we are creating or validating infrastructure
        /// Or the TopicARN if we are assuming infrastructure exists
        /// </summary>
        /// <value>The name.</value>
        public RoutingKey RoutingKey { get; set; } 
    }
}
