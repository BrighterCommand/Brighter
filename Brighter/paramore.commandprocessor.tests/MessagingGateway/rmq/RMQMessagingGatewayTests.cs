using System;
using System.Text;
using Machine.Specifications;
using RabbitMQ.Client;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;

namespace paramore.commandprocessor.tests.MessagingGateway.rmq
{
    [Subject("Messaging Gateway")]
    public class When_posting_a_message_via_the_messaging_gateway
    {
        static IAmAMessagingGateway messagingGateway;
        static Message message;
        static TestRMQListener client;
        static string messageBody;

        Establish context = () =>
            {
                messagingGateway = new RMQMessagingGateway();
                message = new Message(
                    header: new MessageHeader(Guid.NewGuid(), "test"), 
                    body:new MessageBody("test content")
                    );

                client = new TestRMQListener(message.Header.Topic);
            };

        Because of = () =>
            {
                messagingGateway.SendMessage(message);
                messageBody = client.Listen();
            };

        It should_send_a_message_via_rmq_with_the_matching_body = () => messageBody.ShouldEqual(message.Body.Value);
    }

    internal class TestRMQListener
    {
        readonly string channelName;
        readonly ConnectionFactory connectionFactory;
        readonly IConnection connection;
        readonly IModel channel;

        public TestRMQListener(string channelName)
        {
            this.channelName = channelName;
            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration(); 
            connectionFactory = new ConnectionFactory{Uri = configuration.AMPQUri.Uri.ToString()};
            connection = connectionFactory.CreateConnection();
            channel = connection.CreateModel();
            channel.QueueDeclare(this.channelName, false, false, false, null);
        }

        public string Listen()
        {
            try
            {
                var result = channel.BasicGet(this.channelName, true);
                if (result != null)
                {
                    var message = Encoding.UTF8.GetString(result.Body);
                    return message;
                }
            }
            finally 
            {
                channel.Close();
                connection.Close();
            }
            return null;
        }
    }
}
