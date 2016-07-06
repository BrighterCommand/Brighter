// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.awssqs
// Author           : ian
// Created          : 08-17-2015
//
// Last Modified By : ian
// Last Modified On : 10-25-2015
// ***********************************************************************
// <copyright file="SqsMessageConsumerFactory.cs" company="">
//     Copyright ©  2015
// </copyright>
// <summary></summary>
// ***********************************************************************
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    /// <summary>
    /// Class SqsMessageConsumerFactory.
    /// </summary>
    public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// </summary>
        public SqsMessageConsumerFactory() 
           : this(LogProvider.For<SqsMessageConsumerFactory>())
        {}

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// Use this if you need to inject the logger, for example for testing
        /// </summary>
        /// <param name="logger">The logger.</param>
        public SqsMessageConsumerFactory(ILog logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">if set to <c>true</c> [is durable].</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable)
        {
            return new SqsMessageConsumer(channelName, _logger);
        }
    }
}