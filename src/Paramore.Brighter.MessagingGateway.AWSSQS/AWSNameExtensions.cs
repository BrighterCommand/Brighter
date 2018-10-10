namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public static class AWSNameExtensions
    {
        public static ChannelName ToValidSQSQueueName(this ChannelName channelName, bool isFifo = false)
        {
            //SQS only allows 80 characters alphanumeric, hyphens, and underscores, but we might use a period in a 
            //default typename strategy
            var name = channelName.Value;
            name = name.Replace(".", "_");
            if (name.Length > 80)
                name = name.Substring(0, 80);

            if (isFifo)
            {
                name = name + ".fifo";
            }

            return new ChannelName(name);
        }

        public static RoutingKey ToValidSNSTopicName(this RoutingKey routingKey)
        {
            //SNS only opic names are limited to 256 characters. Alphanumeric characters plus hyphens (-) and
            //underscores (_) are allowed. Topic names must be unique within an AWS account.
            var topic = routingKey.Value;
            topic = topic.Replace(".", "_");
            if (topic.Length > 256)
                topic = topic.Substring(0, 256);
            
            return new RoutingKey(topic);
        }
    }
}
