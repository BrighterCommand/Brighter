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
using paramore.brighter.commandprocessor.messaginggateway.restms;
using paramore.brighter.commandprocessor.messaginggateway.restms.MessagingGatewayConfiguration;
using paramore.brighter.commandprocessor.messaginggateway.rmq;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.restms
{
    [Subject("Messaging Gateway")]
    [Category("RESTMS")]
    public class When_posting_a_message_via_the_messaging_gateway : ContextSpecification
    {
        private const string TOPIC = "test";
        private static IAmAMessageProducer s_messageProducer;
        private static IAmAMessageConsumer s_messageConsumer;
        private static Message s_message;
        private static Message s_sentMessage;
        private static string s_messageBody;
        private const string QUEUE_NAME = "test";

        private Establish _context = () =>
        {
            var logger = LogProvider.For<RmqMessageConsumer>();
            var configuration = new RestMSMessagingGatewayConfiguration
            {
                Feed = new Feed { Name = "test", Type = "Default"},
                RestMS = new RestMsSpecification { Uri = new Uri("http://localhost:3416/restms/domain/default"),  Id = "dh37fgj492je", User ="Guest", Key ="wBgvhp1lZTr4Tb6K6+5OQa1bL9fxK7j8wBsepjqVNiQ=", Timeout=2000}
            };
            s_messageProducer = new RestMsMessageProducer(configuration, logger);
            s_messageConsumer = new RestMsMessageConsumer(configuration, QUEUE_NAME, TOPIC, logger);
            s_message = new Message(
                header: new MessageHeader(Guid.NewGuid(), TOPIC, MessageType.MT_COMMAND),
                body: new MessageBody("test content")
                );
        };

        private Because _of = () =>
        {
            s_messageConsumer.Receive(30000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            s_messageProducer.Send(s_message);
            s_sentMessage = s_messageConsumer.Receive(30000);
            s_messageBody = s_sentMessage.Body.Value;
            s_messageConsumer.Acknowledge(s_sentMessage);
        };

        private It _should_send_a_message_via_restms_with_the_matching_body = () => s_messageBody.ShouldEqual(s_message.Body.Value);
        private It _should_have_an_empty_pipe_after_acknowledging_the_message = () => ((RestMsMessageConsumer)s_messageConsumer).NoOfOutstandingMessages(30000).ShouldEqual(0);

        private Cleanup _tearDown = () =>
        {
            s_messageConsumer.Purge();
            s_messageProducer.Dispose();
        };
    }
}