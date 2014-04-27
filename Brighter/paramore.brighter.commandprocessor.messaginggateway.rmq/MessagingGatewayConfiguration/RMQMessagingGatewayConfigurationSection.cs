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
using System.Configuration;

namespace paramore.brighter.commandprocessor.messaginggateway.rmq.MessagingGatewayConfiguration
{
    public class RMQMessagingGatewayConfigurationSection : ConfigurationSection
    {
        public static RMQMessagingGatewayConfigurationSection GetConfiguration()
        {
            var configuration = ConfigurationManager.GetSection("rmqMessagingGateway")as RMQMessagingGatewayConfigurationSection ;

            if (configuration != null)
                return configuration;

            return new RMQMessagingGatewayConfigurationSection();
        }

        [ConfigurationProperty("amqpUri")]
        public AMQPUriSpecification AMPQUri
        {
            get { return this["amqpUri"] as AMQPUriSpecification ; }
            set { this["amqpUri"] = value; }
        }

        [ConfigurationProperty("exchange", IsRequired = true)]
        public Exchange Exchange
        {
            get { return this["exchange"] as Exchange; }
            set { this["exchange"] = value; }
        }
    }

    public class AMQPUriSpecification : ConfigurationElement
    {
        [ConfigurationProperty("uri", DefaultValue = "amqp://guest:guest@localhost:5672/%2f", IsRequired = true)]
        public Uri Uri
        {
            get { return (Uri)this["uri"]; }
            set { this["uri"] = value; }
        }
        
    }

    public class Exchange : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "", IsRequired = true)]
        public string Name
        {
            get { return this["name"] as string; }
            set { this["name"] = value; }
        }
    }
}
