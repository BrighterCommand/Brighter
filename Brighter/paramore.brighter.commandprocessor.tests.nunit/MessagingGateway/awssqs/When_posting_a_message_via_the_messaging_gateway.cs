using System;
using Amazon.Runtime;
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    public partial class AWSSQSMessagingGatewayTests {
        
        [Category("AWS")]
        public class When_posting_a_message_via_the_messaging_gateway : ContextSpecification
        {
            private static string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
            private static Message _message;
            private static SqsMessageProducer _messageProducer;
            private static TestAWSQueueListener _queueListener;
            private static Amazon.SQS.Model.Message _listenedMessage;

            private Establish context = () =>
            {
                _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials(), queueUrl);
                _message = new Message(header: new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

                var credentials = new AnonymousAWSCredentials();
                _messageProducer = new SqsMessageProducer(credentials);
            };

            private Because of = () =>
            {
                _messageProducer.Send(_message);
                _listenedMessage = _queueListener.Listen();
            };

            private It should_send_the_message_to_aws_sqs = () => _listenedMessage.Body.ShouldNotBeNull();

            private Cleanup queue = () => _queueListener.DeleteMessage(_listenedMessage.ReceiptHandle);
        }
    }
}