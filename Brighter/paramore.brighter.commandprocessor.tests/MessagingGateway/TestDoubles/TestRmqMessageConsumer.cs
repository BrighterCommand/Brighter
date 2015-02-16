using System;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messaginggateway.rmq;
using RabbitMQ.Client.Exceptions;

namespace paramore.commandprocessor.tests.MessagingGateway.TestDoubles
{
    /*
     * Use to force a failure mirroring a RabbitMQ connection failure for testing flow of failure
     */

    class TestRmqMessageConsumer : RmqMessageConsumer 
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageGateway" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public TestRmqMessageConsumer(string queueName, string routingKey, ILog logger) : base(queueName, routingKey, logger) {}

        #region Overrides of MessageGateway

        protected override void ConnectToBroker()
        {
            throw new BrokerUnreachableException(new Exception("Force Test Failure"));
        }

        #endregion
    }
}
