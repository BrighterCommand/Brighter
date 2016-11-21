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
using nUnitShouldAdapter;
using NUnit.Framework;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.TestDoubles;
using RabbitMQ.Client.Exceptions;
using Catch = NUnit.Specifications.Catch;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.rmq 
{
    
    [Category("RMQ")]
    public class When_a_message_consumer_throws_an_already_closed_exception_when_connecting: ContextSpecification
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

            var rmqConnection = new RmqMessagingGatewayConnection()
            {
                AmpqUri = new AmqpUriSpecification(uri: new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };


            s_sender = new RmqMessageProducer(rmqConnection, logger);
            s_receiver = new RmqMessageConsumer(rmqConnection, s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, false, 1, false, logger);
            s_badReceiver = new AlreadyClosedRmqMessageConsumer(rmqConnection, s_sentMessage.Header.Topic, s_sentMessage.Header.Topic, false, 1, false, logger);

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
}