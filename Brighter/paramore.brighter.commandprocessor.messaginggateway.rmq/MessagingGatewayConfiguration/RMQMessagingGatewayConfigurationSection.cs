


// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 12-18-2014
//
// Last Modified By : ian
// Last Modified On : 01-02-2015
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using RabbitMQ.Client;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration
{
    /// <summary>
    /// Class RMQMessagingGatewayConfigurationSection.
    /// </summary>
    public class RMQMessagingGatewayConfigurationSection
    {
        /// <summary>
        /// Gets or sets the ampq URI.
        /// </summary>
        /// <value>The ampq URI.</value>
        public AMQPUriSpecification AMPQUri { get; set; }

        /// <summary>
        /// Gets or sets the exchange.
        /// </summary>
        /// <value>The exchange.</value>
        public Exchange Exchange { get; set; }

        public Queues Queues { get; set; }

        public IEnumerable<Connection> Connections { get; set; }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <returns>RMQMessagingGatewayConfigurationSection.</returns>
        //public static RMQMessagingGatewayConfigurationSection GetConfiguration()
        //{
        //    var configuration = ConfigurationManager.GetSection("rmqMessagingGateway") as RMQMessagingGatewayConfigurationSection;

        //    if (configuration != null)
        //    {
        //        var foundConfiguration = configuration.Connections;
        //        if (foundConfiguration.Count == 0)
        //        {
        //            return configuration;
        //        }
        //        if (foundConfiguration.Count == 1)
        //        {
        //            var onlyConnection = foundConfiguration.GetConnection();
        //            return new RMQMessagingGatewayConfigurationSection
        //            {
        //                AMPQUri = onlyConnection.AMPQUri,
        //                Exchange = onlyConnection.Exchange,
        //                Queues = onlyConnection.Queues
        //            };
        //        }
        //        throw new ConfigurationException(string.Format("Found {0} rmq Configuration sections, but no name provided", foundConfiguration.Count));
        //    }

        //    return new RMQMessagingGatewayConfigurationSection();
        //}

        //public static RMQMessagingGatewayConfigurationSection GetConfiguration(string connectionName)
        //{
        //    if (string.IsNullOrWhiteSpace(connectionName)) return GetConfiguration();

        //    //var configuration = ConfigurationManager.GetSection("rmqMessagingGateway") as RMQMessagingGatewayConfigurationSection;

        //    if (configuration != null)
        //    {
        //        var foundConfiguration = configuration.Connections.GetConnection(connectionName);
        //        if (foundConfiguration != null)
        //        {
        //            return new RMQMessagingGatewayConfigurationSection
        //            {
        //                AMPQUri = foundConfiguration.AMPQUri,
        //                Exchange = foundConfiguration.Exchange, 
        //                Queues = foundConfiguration.Queues
        //            };
        //        }
        //    }
        //    return new RMQMessagingGatewayConfigurationSection();
        //}
    }

    //public class Connections
    //{
    //    protected override Connection CreateNewElement()
    //    {
    //        return new Connection();
    //    }

    //    protected override Connection CreateNewElement(string elementName)
    //    {
    //        return new Connection {Name = elementName};
    //    }

    //    protected override object GetElementKey(Connection element)
    //    {
    //        return ((Connection) element).Name;
    //    }

    //    public Connection GetConnection(string connectionName)
    //    {
    //        return base.BaseGet(connectionName) as Connection;
    //    }

    //    internal Connection GetConnection()
    //    {
    //        return BaseGet(0) as Connection;
    //    }
    //}

    public class Connection 
    {
        /// <summary>
        /// Sets Unique name for the connection
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ampq URI.
        /// </summary>
        /// <value>The ampq URI.</value>
        public AMQPUriSpecification AMPQUri { get; set; }

        /// <summary>
        /// Gets or sets the exchange.
        /// </summary>
        /// <value>The exchange.</value>
        public Exchange Exchange { get; set; }

        public Queues Queues { get; set; }
    }

    /// <summary>
    /// Gets or Sets the general settings for queues
    /// </summary>
    public class Queues
    {
        /// <summary>
        /// When true, default false, all queues are mirrored across all nodes in the cluster. When a new node is added to the cluster, the queue will be mirrored to that node.
        /// </summary>
        public bool HighAvailability { get; set; }

        /// <summary>
        /// Allow you to limit the number of unacknowledged messages on a channel (or connection) when consuming (aka "prefetch count"). Applied separately to each new consumer on the channel
        /// </summary>
        //DefaultValue = (ushort)1
        public ushort QosPrefetchSize { get; set; }
    }

    /// <summary>
    /// Class AMQPUriSpecification.{CC2D43FA-BBC4-448A-9D0B-7B57ADF2655C}
    /// </summary>
    public class AMQPUriSpecification
    {
        private string _sanitizedUri = null;

        /// <summary>
        /// Gets or sets the URI.
        /// </summary>
        /// <value>The URI.</value>
        //[ConfigurationProperty("uri", DefaultValue = "amqp://guest:guest@localhost:5672/%2f", IsRequired = true)]
        public Uri Uri { get; set; }

        public string GetSanitizedUri()
        {
            if (_sanitizedUri == null)
            {
                var uri = Uri.ToString();
                var positionOfSlashSlash = uri.IndexOf("//") + 2;
                var usernameAndPassword = uri.Substring(positionOfSlashSlash, uri.IndexOf('@') - positionOfSlashSlash);
                _sanitizedUri = uri.Replace(usernameAndPassword, "*****");
            }

            return _sanitizedUri;
        }

        /// <summary>
        /// Gets or sets the retry count for when a connection fails
        /// </summary>
        // DefaultValue = "3", IsRequired = false)]
        public int ConnectionRetryCount { get; set; }

        /// <summary>
        /// The time in milliseconds to wait before retrying to connect again
        /// </summary>
        // DefaultValue = "1000", IsRequired = false)]
        public int RetryWaitInMilliseconds { get; set; }

        /// <summary>
        /// The time in milliseconds to wait before retrying to connect again
        /// </summary>
        // DefaultValue = "60000", IsRequired = false)]
        public int CircuitBreakTimeInMilliseconds { get; set; }
    }

    /// <summary>
    /// Class Exchange.
    /// </summary>
    public class Exchange
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
// DefaultValue = ExchangeType.Direct)]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Exchange"/> is durable.
        /// </summary>
        /// <value><c>true</c> if durable; otherwise, <c>false</c>.</value>
        public bool Durable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the declared <see cref="Exchange"/> support delayed messages.
        /// (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        /// <value><c>true</c> if supporting; otherwise, <c>false</c>.</value>
        public bool SupportDelay { get; set; }
    }
}