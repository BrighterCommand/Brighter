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
using System.Linq;
using System.Threading.Tasks;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration;
using nUnitShouldAdapter;

namespace paramore.commandprocessor.tests.MessagingGateway.rmq
{
    [Subject("Messaging Gateway")]
    [Tags("Requires", new[] { "RabbitMQ", "RabbitMQProducerReceiver" })]
    public class When_multiple_threads_try_to_post_a_message_at_the_same_time
    {
        private static IAmAMessageProducer s_messageProducer;
        private static Message s_message;
        private static string s_messageBody;
        private static IDictionary<string, object> s_messageHeaders;

        private Establish _context = () =>
        {
            var logger = LogProvider.For<RmqMessageConsumer>();

            s_message = new Message(header: new MessageHeader(Guid.NewGuid(), "nonexistenttopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(uri: new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            s_messageProducer = new RmqMessageProducer(rmqConnection, logger);
        };

        private Because _of = () =>
        {
            Parallel.ForEach(Enumerable.Range(0, 10), _ =>
            {
                s_messageProducer.Send(s_message);
            });
        };

        It _should_not_throw = () => { };

        private Cleanup _tearDown = () =>
        {
            s_messageProducer.Dispose();
        };
    }
}