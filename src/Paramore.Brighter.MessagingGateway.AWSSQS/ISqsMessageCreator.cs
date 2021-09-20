using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal interface ISqsMessageCreator
    {
        Message CreateMessage(Amazon.SQS.Model.Message sqsMessage);
    }

    internal class SqsMessageCreatorBase
    {
        protected HeaderResult<string> ReadReceiptHandle(Amazon.SQS.Model.Message sqsMessage)
        {
            if (sqsMessage.ReceiptHandle != null)
            {
                return new HeaderResult<string>(sqsMessage.ReceiptHandle, true);
            }
            return new HeaderResult<string>(string.Empty, true);
        }

        protected Message FailureMessage(HeaderResult<string> topic, HeaderResult<Guid> messageId)
        {
            var header = new MessageHeader(
                messageId.Success ? messageId.Result : Guid.Empty,
                topic.Success ? topic.Result : string.Empty,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }
    }
}
