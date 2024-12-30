#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

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

        protected Message FailureMessage(HeaderResult<RoutingKey> topic, HeaderResult<string?> messageId)
        {
            var id = messageId.Success ? messageId.Result : string.Empty;
            var routingKey = topic.Success ? topic.Result : RoutingKey.Empty;
            
            var header = new MessageHeader(
                id!,
                routingKey!,
                MessageType.MT_UNACCEPTABLE);
            var message = new Message(header, new MessageBody(string.Empty));
            return message;
        }
    }
}
