using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SnsAttributes
    {
        /// <summary>
        /// The ARN for a topic - the routing key forms the trailing part of this
        /// i.e. arn:aws:sns:us-east-2:123456789012:MyTopic -- the routing key is MyTopic
        /// If the TopicARN is set we assume external creation - we will use the topic at this ARN and
        /// ignore other fields
        /// </summary>
        public string TopicARN { get; set; } = null;

        /// <summary>
        /// The policy that defines how Amazon SNS retries failed deliveries to HTTP/S endpoints
        /// Ignored if TopicARN is set
        /// </summary>
        public string DeliveryPolicy { get; set; } = null;

        /// <summary>
        /// The JSON serialization of the topic's access control policy.
        /// The policy that defines who can access your topic. By default, only the topic owner can publish or subscribe to the topic.
        /// Ignored if TopicARN is set
        /// </summary>
        public string Policy { get; set; } = null;
        
        /// <summary>
        /// A list of resource tags to use when creating the publication
        /// Ignored if TopicARN is set
        /// </summary>
        public List<Tag> Tags => new List<Tag>();
    }
}
