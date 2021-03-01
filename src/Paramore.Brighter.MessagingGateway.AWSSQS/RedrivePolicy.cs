namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// Encapsulates the parameters for a redrive policy for an SQS queue
    /// </summary>
    public class RedrivePolicy
    {
        /// <summary>
        /// The maximum number of requeues for a message before we push it to the DLQ instead 
        /// </summary>
        public int MaxReceiveCount { get; set; }
        
        /// <summary>
        /// The name of the dead letter queue we want to associate with any redrive policy
        /// </summary>
        public ChannelName DeadlLetterQueueName { get; set; }

        /// <summary>
        /// The policy that puts an upper limit on requeues before moving to a DLQ 
        /// </summary>
        /// <param name="deadlLetterQueueName">The name of any dead letter queue used by a redrive policy</param>
        /// <param name="maxReceiveCount">The maximum number of retries before we push to a DLQ</param>
        public RedrivePolicy(string deadLetterQueueName, int maxReceiveCount)
        {
            MaxReceiveCount = maxReceiveCount;
            DeadlLetterQueueName = new ChannelName(deadLetterQueueName);
        }
    }
}
