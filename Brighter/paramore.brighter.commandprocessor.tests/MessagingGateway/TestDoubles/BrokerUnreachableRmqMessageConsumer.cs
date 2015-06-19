#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace paramore.commandprocessor.tests.MessagingGateway.TestDoubles
{
    /*
     * Use to force a failure mirroring a RabbitMQ connection failure for testing flow of failure
     */

    internal class BrokerUnreachableRmqMessageConsumer : RmqMessageConsumer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="routingKey"></param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public BrokerUnreachableRmqMessageConsumer(string queueName, string routingKey, bool isDurable, ILog logger) : base(queueName, routingKey, isDurable, logger) { }

        protected override void ConnectToBroker()
        {
            throw new BrokerUnreachableException(new Exception("Force Test Failure"));
        }
    }

    internal class AlreadyClosedRmqMessageConsumer : RmqMessageConsumer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="routingKey"></param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public AlreadyClosedRmqMessageConsumer(string queueName, string routingKey, bool isDurable, ILog logger) : base(queueName, routingKey, isDurable, logger) { }

        protected override void CreateConsumer()
        {
            throw new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test"));
        }
    }

    internal class OperationInterruptedRmqMessageConsumer : RmqMessageConsumer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="routingKey"></param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public OperationInterruptedRmqMessageConsumer(string queueName, string routingKey, bool isDurable, ILog logger) : base(queueName, routingKey, isDurable, logger) { }

        protected override void CreateConsumer()
        {
            throw new OperationInterruptedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test"));
        }
    }

    internal class NotSupportedRmqMessageConsumer : RmqMessageConsumer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="routingKey"></param>
        /// <param name="isDurable"></param>
        /// <param name="logger">The logger.</param>
        public NotSupportedRmqMessageConsumer(string queueName, string routingKey, bool isDurable, ILog logger) : base(queueName, routingKey, isDurable, logger) { }

        protected override void CreateConsumer()
        {
            throw new NotSupportedException();
        }
    }
}
