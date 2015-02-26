#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Text;
using System.Threading.Tasks;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.MessagingGateway.TestDoubles;
using RabbitMQ.Client;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using RabbitMQ.Client.Exceptions;

namespace paramore.commandprocessor.tests.MessagingGateway.rmq
{
    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_posting_a_message_via_the_messaging_gateway
    {
        private static IAmAMessageProducer s_messageProducer;
        private static IAmAMessageConsumer s_messageConsumer;
        private static Message s_message;
        private static TestRMQListener s_client;
        private static string s_messageBody;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<RmqMessageConsumer>();

            s_message = new Message(header: new MessageHeader(Guid.NewGuid(), "test1", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            s_messageProducer = new RmqMessageProducer(logger);
            s_messageConsumer = new RmqMessageConsumer(s_message.Header.Topic, s_message.Header.Topic, logger);

            s_client = new TestRMQListener(s_message.Header.Topic);
            s_messageConsumer.Purge();
        };

        private Because _of = () =>
        {
            s_messageProducer.Send(s_message);
            s_messageBody = s_client.Listen();
        };

        private It _should_send_a_message_via_rmq_with_the_matching_body = () => s_messageBody.ShouldEqual(s_message.Body.Value);

        private Cleanup _tearDown = () =>
        {
            s_messageConsumer.Purge();
            s_messageProducer.Dispose();
        };
    }

    internal class TestRMQListener
    {
        private readonly string _channelName;
        private readonly ConnectionFactory _connectionFactory;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public TestRMQListener(string channelName)
        {
            _channelName = channelName;
            var configuration = RMQMessagingGatewayConfigurationSection.GetConfiguration();
            _connectionFactory = new ConnectionFactory { Uri = configuration.AMPQUri.Uri.ToString() };
            _connection = _connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(configuration.Exchange.Name, ExchangeType.Direct, false);
            _channel.QueueDeclare(_channelName, false, false, false, null);
            _channel.QueueBind(_channelName, configuration.Exchange.Name, _channelName);
        }

        public string Listen()
        {
            try
            {
                var result = _channel.BasicGet(_channelName, true);
                if (result != null)
                {
                    _channel.BasicAck(result.DeliveryTag, false);
                    var message = Encoding.UTF8.GetString(result.Body);
                    return message;
                }
            }
            finally
            {
                //Added wait as rabbit needs some time to sort it self out and the close and dispose was happening to quickly
                Task.Delay(200).Wait();
                _channel.Dispose();
                if (_connection.IsOpen) _connection.Dispose();
            }
            return null;
        }
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_reading_a_message_via_the_messaging_gateway
    {
        private static IAmAMessageProducer s_sender;
        private static IAmAMessageConsumer s_receiver;
        private static Message s_sentMessage;
        private static Message s_receivedMessage;

        private Establish _context = () =>
        {
            var testGuid = Guid.NewGuid();
            var logger = LogProvider.For<RmqMessageConsumer>();

            var messageHeader = new MessageHeader(Guid.NewGuid(), "test2", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            s_sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            s_sender = new RmqMessageProducer(logger);
            s_receiver = new RmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);

            s_receiver.Purge();

            //create queue if missing
            s_receiver.Receive(1);
        };

        private Because _of = () =>
        {
            s_sender.Send(s_sentMessage);
            s_receivedMessage = s_receiver.Receive(2000);
            s_receiver.Acknowledge(s_receivedMessage);
        };

        private It _should_send_a_message_via_rmq_with_the_matching_body = () => s_receivedMessage.ShouldEqual(s_sentMessage);

        private Cleanup _teardown = () =>
        {
            s_receiver.Purge();
            s_sender.Dispose();
            s_receiver.Dispose();
        };
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_a_message_consumer_throws_an_already_closed_exception_when_connecting_should_retry_until_circuit_breaks
    {
        private static IAmAMessageProducer s_sender;
        private static IAmAMessageConsumer s_receiver;
        private static IAmAMessageConsumer s_badReceiver;
        private static Message s_sentMessage;
        private static Exception s_expectedException;
        private static Exception s_firstException;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<BrokerUnreachableRmqMessageConsumer>();

            var messageHeader = new MessageHeader(Guid.NewGuid(), "test2", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            s_sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            s_sender = new RmqMessageProducer(logger);
            s_receiver = new RmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);
            s_badReceiver = new AlreadyClosedRmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);

            s_receiver.Purge();
            s_sender.Send(s_sentMessage);
        };

        private Because _of = () =>
                     {
                         s_firstException = Catch.Exception(() => s_badReceiver.Receive(2000));
                     };

        private It _should_return_a_channel_failure_exception = () => s_firstException.ShouldBeOfExactType<ChannelFailureException>();
        private It _should_return_an_explainging_inner_exception = () => s_firstException.InnerException.ShouldBeOfExactType<AlreadyClosedException>();

        private Cleanup _teardown = () =>
        {
            s_receiver.Purge();
            s_sender.Dispose();
            s_receiver.Dispose();
        };
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_a_message_consumer_throws_an_operation_interrupted_exception_when_connecting_should_retry_until_circuit_breaks
    {
        private static IAmAMessageProducer s_sender;
        private static IAmAMessageConsumer s_receiver;
        private static IAmAMessageConsumer s_badReceiver;
        private static Message s_sentMessage;
        private static Exception s_expectedException;
        private static Exception s_firstException;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<BrokerUnreachableRmqMessageConsumer>();

            var messageHeader = new MessageHeader(Guid.NewGuid(), "test2", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            s_sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            s_sender = new RmqMessageProducer(logger);
            s_receiver = new RmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);
            s_badReceiver = new OperationInterruptedRmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);

            s_receiver.Purge();
            s_sender.Send(s_sentMessage);
        };

        private Because _of = () =>
                     {
                         s_firstException = Catch.Exception(() => s_badReceiver.Receive(2000));
                     };

        private It _should_return_a_channel_failure_exception = () => s_firstException.ShouldBeOfExactType<ChannelFailureException>();
        private It _should_return_an_explainging_inner_exception = () => s_firstException.InnerException.ShouldBeOfExactType<OperationInterruptedException>();

        private Cleanup _teardown = () =>
        {
            s_receiver.Purge();
            s_sender.Dispose();
            s_receiver.Dispose();
        };
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_a_message_consumer_throws_an_not_supported_exception_when_connecting_should_retry_until_circuit_breaks
    {
        private static IAmAMessageProducer s_sender;
        private static IAmAMessageConsumer s_receiver;
        private static IAmAMessageConsumer s_badReceiver;
        private static Message s_sentMessage;
        private static Exception s_expectedException;
        private static Exception s_firstException;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<BrokerUnreachableRmqMessageConsumer>();

            var messageHeader = new MessageHeader(Guid.NewGuid(), "test2", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            s_sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            s_sender = new RmqMessageProducer(logger);
            s_receiver = new RmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);
            s_badReceiver = new NotSupportedRmqMessageConsumer(s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, logger);

            s_receiver.Purge();
            s_sender.Send(s_sentMessage);
        };

        private Because _of = () =>
                     {
                         s_firstException = Catch.Exception(() => s_badReceiver.Receive(2000));
                     };

        private It _should_return_a_channel_failure_exception = () => s_firstException.ShouldBeOfExactType<ChannelFailureException>();
        private It _should_return_an_explainging_inner_exception = () => s_firstException.InnerException.ShouldBeOfExactType<NotSupportedException>();

        private Cleanup _teardown = () =>
        {
            s_receiver.Purge();
            s_sender.Dispose();
            s_receiver.Dispose();
        };
    }
}