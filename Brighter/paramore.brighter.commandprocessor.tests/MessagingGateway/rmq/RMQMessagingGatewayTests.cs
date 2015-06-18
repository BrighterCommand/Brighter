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
using System.Collections.Generic;
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
using System.Threading;

namespace paramore.commandprocessor.tests.MessagingGateway.rmq
{
    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ", "RabbitMQProducerReceiver" })]
    public class When_posting_a_message_via_the_messaging_gateway
    {
        private static IAmAMessageProducer s_messageProducer;
        private static IAmAMessageConsumer s_messageConsumer;
        private static Message s_message;
        private static TestRMQListener s_client;
        private static string s_messageBody;
        private static IDictionary<string, object> s_messageHeaders;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<RmqMessageConsumer>();

            s_message = new Message(header: new MessageHeader(Guid.NewGuid(), "test1", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            s_messageProducer = new RmqMessageProducer(logger);
            s_messageConsumer = new RmqMessageConsumer(s_message.Header.Topic, s_message.Header.Topic, logger);
            s_messageConsumer.Purge();

            s_client = new TestRMQListener(s_message.Header.Topic);
        };

        private Because _of = () =>
        {
            s_messageProducer.Send(s_message);

            var result = s_client.Listen();
            s_messageBody = result.GetBody();
            s_messageHeaders = result.GetHeaders();
        };

        private It _should_send_a_message_via_rmq_with_the_matching_body = () => s_messageBody.ShouldEqual(s_message.Body.Value);
        private It _should_send_a_message_via_rmq_without_delay_header = () => s_messageHeaders.Keys.ShouldNotContain(HeaderNames.DELAY_MILLISECONDS);
        private It _should_received_a_message_via_rmq_without_delayed_header = () => s_messageHeaders.Keys.ShouldNotContain(HeaderNames.DELAYED_MILLISECONDS);

        private Cleanup _tearDown = () =>
        {
            s_messageConsumer.Purge();
            s_messageProducer.Dispose();
        };
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ", "RabbitMQProducerReceiver", "RabbitMQDelayed" })]
    //[Ignore("This only works if RabbitMQ 3.5 w/plugin rabbitmq_delayed_message_exchange")]
    public class When_reading_a_delayed_message_via_the_messaging_gateway
    {
        private static IAmAMessageProducerSupportingDelay s_messageProducer;
        private static IAmAMessageConsumer s_messageConsumer;
        private static Message s_message;
        private static TestRMQListener s_client;
        private static string s_messageBody;
        private static bool s_immediateReadIsNull;
        private static IDictionary<string, object> s_messageHeaders;

        private Establish _context = () =>
        {
            using (AppConfig.Change("app.with-delay.config"))
            {
                var logger = LogProvider.For<RmqMessageConsumer>();

                var s_header = new MessageHeader(Guid.NewGuid(), "test3", MessageType.MT_COMMAND);
                var s_originalMessage = new Message(header: s_header, body: new MessageBody("test3 content"));

                var s_mutatedHeader = new MessageHeader(s_header.Id, "test3", MessageType.MT_COMMAND);
                s_mutatedHeader.Bag.Add(HeaderNames.DELAY_MILLISECONDS, 1000);
                s_message = new Message(header: s_mutatedHeader, body: s_originalMessage.Body);

                s_messageProducer = new RmqMessageProducer(logger);
                s_messageConsumer = new RmqMessageConsumer(s_message.Header.Topic, s_message.Header.Topic, logger);
                s_messageConsumer.Purge();

                s_client = new TestRMQListener(s_message.Header.Topic);
            }
        };

        private Because _of = () =>
        {
            s_messageProducer.Send(s_message, 1000);

            var immediateResult = s_client.Listen(waitForMilliseconds: 0, suppressDisposal: true);
            s_immediateReadIsNull = immediateResult == null;

            var delayedResult = s_client.Listen(waitForMilliseconds: 2000);
            s_messageBody = delayedResult.GetBody();
            s_messageHeaders = delayedResult.GetHeaders();
        };

        private It _should_have_not_been_able_get_message_before_delay = () => s_immediateReadIsNull.ShouldBeTrue();
        private It _should_send_a_message_via_rmq_with_the_matching_body = () => s_messageBody.ShouldEqual(s_message.Body.Value);
        private It _should_send_a_message_via_rmq_with_delay_header = () => s_messageHeaders.Keys.ShouldContain(HeaderNames.DELAY_MILLISECONDS);
        private It _should_received_a_message_via_rmq_with_delayed_header = () => s_messageHeaders.Keys.ShouldContain(HeaderNames.DELAYED_MILLISECONDS);

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
            _channel.DeclareExchangeForConfiguration(configuration);
            _channel.QueueDeclare(_channelName, false, false, false, null);
            _channel.QueueBind(_channelName, configuration.Exchange.Name, _channelName);
        }

        public BasicGetResult Listen(int waitForMilliseconds = 0, bool suppressDisposal = false)
        {
            try
            {
                if (waitForMilliseconds > 0)
                    Task.Delay(waitForMilliseconds).Wait();

                var result = _channel.BasicGet(_channelName, true);
                if (result != null)
                {
                    _channel.BasicAck(result.DeliveryTag, false);
                    return result;
                }
            }
            finally
            {
                if (!suppressDisposal)
                {
                    //Added wait as rabbit needs some time to sort it self out and the close and dispose was happening to quickly
                    Task.Delay(200).Wait();
                    _channel.Dispose();
                    if (_connection.IsOpen) _connection.Dispose();
                }
            }
            return null;
        }
    }

    internal static class TestRMQExtensions
    {
        public static string GetBody(this BasicGetResult result)
        {
            return result == null ? null : Encoding.UTF8.GetString(result.Body);
        }

        public static IDictionary<string, object> GetHeaders(this BasicGetResult result)
        {
            return result == null ? null : result.BasicProperties.Headers;
        }
    }

    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ" })]
    public class When_a_message_consumer_throws_an_already_closed_exception_when_connecting_should_retry_until_circuit_breaks
    {
        private static IAmAMessageProducer s_sender;
        private static IAmAMessageConsumer s_receiver;
        private static IAmAMessageConsumer s_badReceiver;
        private static Message s_sentMessage;
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
        private It _should_return_an_explaining_inner_exception = () => s_firstException.InnerException.ShouldBeOfExactType<AlreadyClosedException>();

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