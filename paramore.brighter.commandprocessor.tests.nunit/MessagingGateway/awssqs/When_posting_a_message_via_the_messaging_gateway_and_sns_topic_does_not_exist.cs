using System;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    [Category("AWS")]
    public class When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist : ContextSpecification
    {
        private static Message _message;
        private static SqsMessageProducer _messageProducer;
        private static TestAWSQueueListener _queueListener;
        private static Topic _topic;
        private Establish context = () =>
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials());
            _message = new Message(header: new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
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