using System;
using Common.Logging;
using Common.Logging.Configuration;
using Common.Logging.Simple;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.restms;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace paramore.commandprocessor.tests.MessagingGateway.restms
{
    public class When_posting_a_message_via_the_messaging_gateway
    {
        static IAmAClientRequestHandler clientRequestHandler;
        static IAmAServerRequestHandler serverRequestHandler;
        static Message message;
        static TestRestMSListener client;
        static string messageBody;

        Establish context = () =>
        {
            var properties = new NameValueCollection();
            properties["showDateTime"] = "true";
            LogManager.Adapter = new ConsoleOutLoggerFactoryAdapter(properties);
            var logger = LogManager.GetLogger(typeof (RMQServerRequestHandler));
            clientRequestHandler = new RestMSClientRequestHandler(logger);
            serverRequestHandler = new RestMSServerRequestHandler(logger);
            message = new Message(
                header: new MessageHeader(Guid.NewGuid(), "test", MessageType.MT_COMMAND),
                body: new MessageBody("test content")
                );

            client = new TestRestMSListener(message.Header.Topic);
        };

        Because of = () =>
        {
            clientRequestHandler.Send(message);
            messageBody = client.Listen();
        };

        It should_send_a_message_via_restms_with_the_matching_body = () => messageBody.ShouldEqual(message.Body.Value);

        Cleanup tearDown = () =>
        {
           serverRequestHandler.Purge("test");
           clientRequestHandler.Dispose();
        };
    }

    internal class TestRestMSListener
    {
        public TestRestMSListener(string topic)
        {
        }

        public string Listen()
        {
            return string.Empty;
        }
    }
}