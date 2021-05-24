using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public class SqsPublication : Publication
    {
        /// <summary>
        /// Indicates how we should treat the routing key
        /// TopicFindBy.Arn -> the routing key is an Arn
        /// TopicFindBy.Convention -> The routing key is a name, but use convention to make an Arn for this account
        /// TopicFindBy.Name -> Treat the routing key as a name & use ListTopics to find it (rate limited 30/s)
        /// </summary>

        public TopicFindBy FindTopicBy { get; set; } = TopicFindBy.Convention;
        
        /// <summary>
        /// The attributes of the topic. If TopicARNs is set we will always assume that we do not
        /// need to create or validate the SNS Topic
        /// </summary>
        public SnsAttributes SnsAttributes { get; set; }

        /// <summary>
        /// If we want to use topic Arns and not topics you need to supply a mapping file that tells us
        /// the Arn to use for any message that you send to us, as we use the topic from the header to dispatch to
        /// an Arn.
        /// Internally we construct this routing table when creating on other paths
        /// </summary>
        public Dictionary<string,string> TopicArns { get; set; }
    }
}
