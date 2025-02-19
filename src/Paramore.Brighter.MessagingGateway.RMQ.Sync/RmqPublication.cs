namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    public class RmqPublication : Publication
    {
        /// <summary>
        /// How long should we wait on shutdown for the broker to finish confirming delivery of messages
        /// If we shut down without confirmation then messages will not be marked as sent in the Outbox
        /// Any sweeper will then resend.
        /// </summary>
        public int WaitForConfirmsTimeOutInMilliseconds { get; set; } = 500;
    }
}
