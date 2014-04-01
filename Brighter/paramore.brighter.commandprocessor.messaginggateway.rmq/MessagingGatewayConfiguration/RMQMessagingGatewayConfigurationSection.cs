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
