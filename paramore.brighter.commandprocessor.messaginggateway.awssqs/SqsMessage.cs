using System;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    /// <summary>
    /// This class is used to deserialize a SNS backed SQS message
    /// </summary>
    public class SqsMessage
    {
        public Guid MessageId { get; set; }

        public string Message { get; set; }
    }
}