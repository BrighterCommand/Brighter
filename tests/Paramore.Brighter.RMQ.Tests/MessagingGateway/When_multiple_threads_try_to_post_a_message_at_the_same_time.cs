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
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ;
using Xunit;

namespace Paramore.Brighter.RMQ.Tests.MessagingGateway
{
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerSupportsMultipleThreadsTests : IDisposable
    {
        private readonly IAmAMessageProducer _messageProducer;
        private readonly Message _message;

        public RmqMessageProducerSupportsMultipleThreadsTests()
        {
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), "nonexistenttopic", MessageType.MT_COMMAND), 
                new MessageBody("test content"));

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            _messageProducer = new RmqMessageProducer(rmqConnection);
        }

        [Fact]
        public void When_multiple_threads_try_to_post_a_message_at_the_same_time()
        {
            bool exceptionHappened = false;
            try
            {
                Parallel.ForEach(Enumerable.Range(0, 10), _ =>
                {
                    _messageProducer.Send(_message);
                });
            }
            catch (Exception)
            {
                exceptionHappened = true;
            }

            //_should_not_throw
            exceptionHappened.Should().BeFalse();
        }

        public void Dispose()
        {
            _messageProducer.Dispose();
        }
    }
}
