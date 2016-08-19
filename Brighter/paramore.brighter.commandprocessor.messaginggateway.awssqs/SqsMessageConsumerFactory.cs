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

using Amazon.Runtime;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    /// <summary>
    /// Class SqsMessageConsumerFactory.
    /// </summary>
    public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly AWSCredentials _credentials;

        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// </summary>
        public SqsMessageConsumerFactory(AWSCredentials credentials) 
           : this(credentials, LogProvider.For<SqsMessageConsumerFactory>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
        /// Use this if you need to inject the logger, for example for testing
        /// </summary>
        /// <param name="credentials">The AWS credentials to access the queues</param>
        /// <param name="logger">The logger.</param>
        public SqsMessageConsumerFactory(AWSCredentials credentials, ILog logger)
        {
            _credentials = credentials;
            _logger = logger;
        }

        /// <summary>
        /// Creates the specified queue name.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="isDurable">if set to <c>true</c> [is durable].</param>
        /// <param name="preFetchSize">Number of items to read from the queue at once</param>
        /// <param name="highAvailability">Our are queues high-availablility</param>
        /// <returns>IAmAMessageConsumer.</returns>
        public IAmAMessageConsumer Create(string channelName, string routingKey, bool isDurable, ushort preFetchSize = 1, bool highAvailability = false)
        {
            return new SqsMessageConsumer(_credentials, channelName, _logger);
        }
    }
}