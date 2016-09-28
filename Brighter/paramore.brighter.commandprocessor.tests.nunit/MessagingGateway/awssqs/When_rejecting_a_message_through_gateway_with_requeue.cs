using System;
using Amazon.Runtime;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.commandprocessor.tests.MessagingGateway.awssqs;
using nUnitShouldAdapter;

namespace paramore.brighter.commandprocessor.tests.MessagingGateway.awssqs
{
    public partial class AWSSQSMessagingGatewayTests {
        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_rejecting_a_message_through_gateway_with_requeue
        {
            private static string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message message;
            private static Message _listenedMessage;
            Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                message = new Message(header: messageHeader, body: new MessageBody("test content"));

                var credentials = new AnonymousAWSCredentials();
                sender = new SqsMessageProducer(credentials, logger);
                receiver = new SqsMessageConsumer(credentials, queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(credentials, queueUrl);


                sender.Send(message);

                _listenedMessage = receiver.Receive(1000);
            };

            Because i_reject_the_message = () => receiver.Reject(_listenedMessage, true);

            private It should_requeue_the_message = () =>
            {
                var message = receiver.Receive(1000);
                message.ShouldEqual(_listenedMessage);
            };

            Cleanup the_queue = () => testQueueListener.DeleteMessage(_listenedMessage.Header.Bag["ReceiptHandle"].ToString());

        }
    }
}