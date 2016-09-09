using System;
using Amazon.Runtime;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.commandprocessor.tests.MessagingGateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.MessagingGateway.awssqs
{
    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
    public class When_reading_a_message_via_the_messaging_gateway
    {
        private static TestAWSQueueListener testQueueListener;
        private static IAmAMessageProducer sender;
        private static IAmAMessageConsumer receiver;
        private static Message sentMessage;
        private static Message receivedMessage;
        private static string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        Establish context = () =>
        {
            var logger = LogProvider.For<RmqMessageConsumer>();

            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            sender = new SqsMessageProducer(credentials, logger);
            receiver = new SqsMessageConsumer(credentials, queueUrl, logger);
            testQueueListener = new TestAWSQueueListener(credentials, queueUrl);
        };

        Because of = () =>
        {
            sender.Send(sentMessage);
            receivedMessage = receiver.Receive(2000);
            receiver.Acknowledge(receivedMessage);
        };

        It should_send_a_message_via_sqs_with_the_matching_body = () => receivedMessage.Body.ShouldEqual(sentMessage.Body);
        It should_send_a_message_via_sqs_with_the_matching_header_handled_count = () => receivedMessage.Header.HandledCount.ShouldEqual(sentMessage.Header.HandledCount);
        It should_send_a_message_via_sqs_with_the_matching_header_id = () => receivedMessage.Header.Id.ShouldEqual(sentMessage.Header.Id);
        It should_send_a_message_via_sqs_with_the_matching_header_message_type = () => receivedMessage.Header.MessageType.ShouldEqual(sentMessage.Header.MessageType);
        It should_send_a_message_via_sqs_with_the_matching_header_time_stamp = () => receivedMessage.Header.TimeStamp.ShouldEqual(sentMessage.Header.TimeStamp);
        It should_send_a_message_via_sqs_with_the_matching_header_topic = () => receivedMessage.Header.Topic.ShouldEqual(sentMessage.Header.Topic);
        It should_remove_the_message_from_the_queue = () => testQueueListener.Listen().ShouldBeNull();

        Cleanup the_queue = () => testQueueListener.DeleteMessage(receivedMessage.Header.Bag["ReceiptHandle"].ToString());

    }
}