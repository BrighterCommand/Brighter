using System;
using Amazon.Runtime;
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    
    [Category("AWS")]
    public class When_purging_the_queue : ContextSpecification
    {
        private static TestAWSQueueListener testQueueListener;
        private static IAmAMessageProducer sender;
        private static IAmAMessageConsumer receiver;
        private static Message sentMessage;
        private static string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        private Establish context = () =>
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

        private Because of = () =>
        {
            sender.Send(sentMessage);
            receiver.Purge();
        };

            It should_clean_the_queue = () => testQueueListener.Listen().ShouldBeNull();

    }
}