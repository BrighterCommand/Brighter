namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public static class SQSNameExtensions
    {
        public static ChannelName ToValidSQSQueueName(this ChannelName channelName, bool isFifo = false)
        {
            //SQS only allows 80 characters alphanumeric, hyphens, and underscores, but we might use a period in a 
            //default typename strategy
            var name = channelName.Value;
            name.Replace(".", "_");

            if (isFifo)
            {
                name = name + ".fifo";
            }

            return new ChannelName(name);
        }
    }
}
