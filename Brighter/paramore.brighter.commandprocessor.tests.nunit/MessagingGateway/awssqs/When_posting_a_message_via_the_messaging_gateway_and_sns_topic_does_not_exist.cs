using System;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.commandprocessor.tests.MessagingGateway.awssqs;
using nUnitShouldAdapter;

namespace paramore.brighter.commandprocessor.tests.MessagingGateway.awssqs
{
    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
    public class When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist
    {
        private static Message _message;
        private static SqsMessageProducer _messageProducer;
        private static TestAWSQueueListener _queueListener;
        private static Topic _topic;
        private Establish context = () =>
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials());
            var logger = LogProvider.For<RmqMessageConsumer>();
            _message = new Message(header: new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials, logger);
        };

        private Because of = () =>
        {
            _messageProducer.Send(_message);

            _topic = _queueListener.CheckSnsTopic(_message.Header.Topic);
        };

        It should_create_topic_and_send_the_message = () => _topic.ShouldNotBeNull();

        private Cleanup queue = () => _queueListener.DeleteTopic(_message.Header.Topic);

    }
}