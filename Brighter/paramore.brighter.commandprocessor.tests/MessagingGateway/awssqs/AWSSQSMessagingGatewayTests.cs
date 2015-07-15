using System;
using System.Linq;

using Amazon.SimpleNotificationService.Model;

using Machine.Specifications;

using Newtonsoft.Json;

using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

using Message = paramore.brighter.commandprocessor.Message;

namespace paramore.commandprocessor.tests.MessagingGateway.awssqs
{
    public class AWSSQSMessagingGatewayTests
    {
        private static string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class When_posting_a_message_via_the_messaging_gateway
        {
            private Establish context = () =>
            {
                _queueListener = new TestAWSQueueListener(queueUrl);
                var logger = LogProvider.For<RmqMessageConsumer>();
                _message = new Message(header: new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

                _messageProducer = new SqsMessageProducer(logger);
            };

            private Because of = async () =>
            {
                var task = _messageProducer.Send(_message);
                
                task.ContinueWith(
                    x =>
                    {
                        if (x.IsCompleted)
                        {
                            _listenedMessage = _queueListener.Listen();
                        }
                    }).Wait();
            };

            private It should_send_the_message_to_aws_sqs = () => _listenedMessage.Body.ShouldNotBeNull();

            private Cleanup queue = () => _queueListener.DeleteMessage(_listenedMessage.ReceiptHandle);

            private static Message _message;
            private static SqsMessageProducer _messageProducer;
            private static TestAWSQueueListener _queueListener;
            private static Amazon.SQS.Model.Message _listenedMessage;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist
        {
            private Establish context = () =>
            {
                _queueListener = new TestAWSQueueListener();
                var logger = LogProvider.For<RmqMessageConsumer>();
                _message = new Message(header: new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

                _messageProducer = new SqsMessageProducer(logger);
            };

            private Because of = async () =>
            {
                var task = _messageProducer.Send(_message);

                task.ContinueWith(
                    x =>
                    {
                        if (x.IsCompleted)
                            _topic = _queueListener.CheckSnsTopic(_message.Header.Topic);
                    }).Wait();
            };

            It should_create_topic_and_send_the_message = () => _topic.ShouldNotBeNull();

            private Cleanup queue = () => _queueListener.DeleteTopic(_message.Header.Topic);

            private static Message _message;
            private static SqsMessageProducer _messageProducer;
            private static TestAWSQueueListener _queueListener;
            private static string _listenedMessage;
            private static Topic _topic;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_reading_a_message_via_the_messaging_gateway
        {
            Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

                sender = new SqsMessageProducer(logger);
                receiver = new SqsMessageConsumer(queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(queueUrl);
            };

            Because of = () => sender.Send(sentMessage).ContinueWith(
                x =>
                {
                    receivedMessage = receiver.Receive(2000);
                    receiver.Acknowledge(receivedMessage);
                }).Wait();

            It should_send_a_message_via_sqs_with_the_matching_body = () => receivedMessage.Body.ShouldEqual(sentMessage.Body);
            It should_send_a_message_via_sqs_with_the_matching_header_handled_count = () => receivedMessage.Header.HandledCount.ShouldEqual(sentMessage.Header.HandledCount);
            It should_send_a_message_via_sqs_with_the_matching_header_id = () => receivedMessage.Header.Id.ShouldEqual(sentMessage.Header.Id);
            It should_send_a_message_via_sqs_with_the_matching_header_message_type = () => receivedMessage.Header.MessageType.ShouldEqual(sentMessage.Header.MessageType);
            It should_send_a_message_via_sqs_with_the_matching_header_time_stamp = () => receivedMessage.Header.TimeStamp.ShouldEqual(sentMessage.Header.TimeStamp);
            It should_send_a_message_via_sqs_with_the_matching_header_topic = () => receivedMessage.Header.Topic.ShouldEqual(sentMessage.Header.Topic);
            It should_remove_the_message_from_the_queue = () => testQueueListener.Listen().ShouldBeNull();

            Cleanup the_queue = () => testQueueListener.DeleteMessage(receivedMessage.Header.Bag["ReceiptHandle"].ToString());

            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message sentMessage;
            private static Message receivedMessage;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_rejecting_a_message_through_gateway_with_requeue
        {
            Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                message = new Message(header: messageHeader, body: new MessageBody("test content"));

                sender = new SqsMessageProducer(logger);
                receiver = new SqsMessageConsumer(queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(queueUrl);


                var task = sender.Send(message);

                task.ContinueWith(x =>
                {
                    if (x.IsCompleted) _listenedMessage = receiver.Receive(1000);
                }).Wait();
            };

            Because i_reject_the_message = () => receiver.Reject(_listenedMessage, true);

            private It should_requeue_the_message = () =>
            {
                var message = receiver.Receive(1000);
                message.ShouldEqual(_listenedMessage);
            };

            Cleanup the_queue = () => testQueueListener.DeleteMessage(_listenedMessage.Header.Bag["ReceiptHandle"].ToString());

            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message message;
            private static Message receivedMessage;
            private static Message _listenedMessage;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_rejecting_a_message_through_gateway_without_requeue
        {
            Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                message = new Message(header: messageHeader, body: new MessageBody("test content"));

                sender = new SqsMessageProducer(logger);
                receiver = new SqsMessageConsumer(queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(queueUrl);


                var task = sender.Send(message);

                task.ContinueWith(x =>
                {
                    if (x.IsCompleted) _listenedMessage = receiver.Receive(1000);
                }).Wait();
            };

            Because i_reject_the_message = () => receiver.Reject(_listenedMessage, false);

            private It should_not_requeue_the_message = () =>
            {
                testQueueListener.Listen().ShouldBeNull();
            };

            Cleanup the_queue = () => testQueueListener.DeleteMessage(_listenedMessage.Header.Bag["ReceiptHandle"].ToString());

            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message message;
            private static Message receivedMessage;
            private static Message _listenedMessage;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_purging_the_queue
        {
            private Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

                sender = new SqsMessageProducer(logger);
                receiver = new SqsMessageConsumer(queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(queueUrl);
            };

            Because of = () => sender.Send(sentMessage).ContinueWith(
                x => receiver.Purge()).Wait();

            It should_clean_the_queue = () => testQueueListener.Listen().ShouldBeNull();

            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message sentMessage;
        }

        [Subject("Messaging Gateway")]
        [Tags("Requires", new[] { "AWSSDK", "AWSCredentials" })]
        public class when_requeueing_a_message
        {
            private Establish context = () =>
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

                messageHeader.UpdateHandledCount();
                sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

                sender = new SqsMessageProducer(logger);
                receiver = new SqsMessageConsumer(queueUrl, logger);
                testQueueListener = new TestAWSQueueListener(queueUrl);
            };

            Because of = () => sender.Send(sentMessage).ContinueWith(
                x =>
                {
                    receivedMessage = receiver.Receive(2000);
                    receivedReceiptHandle = receivedMessage.Header.Bag["ReceiptHandle"].ToString();
                    receiver.Requeue(receivedMessage);
                }).Wait();

            It should_delete_the_original_message_and_create_new_message = () =>
            {
                requeuedMessage = receiver.Receive(1000);
                requeuedMessage.Body.Value.ShouldEqual(receivedMessage.Body.Value);
                requeuedMessage.Header.Bag["ReceiptHandle"].ToString().ShouldNotEqual(receivedReceiptHandle);

            };

            Cleanup the_queue = () => testQueueListener.DeleteMessage(requeuedMessage.Header.Bag["ReceiptHandle"].ToString());

            private static TestAWSQueueListener testQueueListener;
            private static IAmAMessageProducer sender;
            private static IAmAMessageConsumer receiver;
            private static Message sentMessage;
            private static Message requeuedMessage;
            private static Message receivedMessage;
            private static string receivedReceiptHandle;
        }
    }

    
}