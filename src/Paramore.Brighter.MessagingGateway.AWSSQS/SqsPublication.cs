namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsPublication : Publication
    {
        /// <summary>
        /// Gets or sets the routing key or topic that this channel subscribes to on the broker.
        /// Either the topic name if we are creating or validating infrastructure
        /// Or the TopicARN if we are assuming infrastructure exists
        /// </summary>
        /// <value>The name.</value>
        public RoutingKey RoutingKey { get; set; } 
        
        /// <summary>
        /// The attributes of the topic. If TopicARN is set we will always assume that we do not
        /// need to create or validate the SNS Topic
        /// </summary>
        public SnsAttributes SnsAttributes { get; set; }
       
    }
}
