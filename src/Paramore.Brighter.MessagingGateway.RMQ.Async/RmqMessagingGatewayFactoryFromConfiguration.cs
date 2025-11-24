using System;
using System.Collections.Generic;
using System.Linq;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Async;

public class RmqMessagingGatewayFactoryFromConfiguration : IAmMessagingGatewayFactoryFromConfiguration
{
    private const string BrighterSection = "Brighter:RabbitMQ";
    private const string AspireSection = "Aspire:RabbitMQ:Client";

    private const string AspireConnection = "ConnectionString";

    public IAmAChannelFactory CreateChannelFactory(IAmAConfiguration configuration, string name, string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
        return new ChannelFactory(new RmqMessageConsumerFactory(connection));
    }

    public IAmAMessageConsumerFactory CreateMessageConsumerFactory(IAmAConfiguration configuration, 
        string name,
        string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();
        return new RmqMessageConsumerFactory(connection);
    }

    public IAmAProducerRegistryFactory CreateProducerRegistryFactory(IAmAConfiguration configuration,
        string name,
        string? sectionName)
    {
        var rabbitMqConfiguration = GetRabbitMqConfiguration(configuration, name, sectionName);
        var connection = rabbitMqConfiguration.Connection.ToMessagingGatewayConnection();

        return new RmqProducerRegistryFactory(connection,
            rabbitMqConfiguration.Publications.Select(x => x.ToPublication()));
    }


    private static RabbitMqConfiguration GetRabbitMqConfiguration(IAmAConfiguration configuration,
        string name,
        string? sectionName)
    {
        if (string.IsNullOrEmpty(sectionName))
        {
            sectionName = BrighterSection;
        }
        
        var configurationSection = configuration.GetSection(sectionName!); 
        var namedConfigurationSection = configurationSection.GetSection(name);
        
        var rabbitMqConfiguration = new RabbitMqConfiguration();
        configurationSection.Bind(rabbitMqConfiguration);
        namedConfigurationSection.Bind(rabbitMqConfiguration);
        
        var aspireConfiguration = configuration.GetSection(AspireSection);
        var namedAspireConfiguration = aspireConfiguration.GetSection(name);
        
        var connection = aspireConfiguration.GetSection(AspireConnection).Get<string>();
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }
        
        connection = namedAspireConfiguration.GetSection(AspireConnection).Get<string>();
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }

        connection = configuration.GetConnectionString(name);
        if (!string.IsNullOrEmpty(connection))
        {
            rabbitMqConfiguration.Connection.AmpqUri ??= new AmqpUriSpecificationConfiguration();
            rabbitMqConfiguration.Connection.AmpqUri.Uri = connection!;
        }

        return rabbitMqConfiguration;
    }
    
    public class RabbitMqConfiguration
    {
        public GatewayConnection Connection { get; set; } = null!;
        public List<RabbitMqSubscriptionConfiguration> Subscriptions { get; set; } = [];
        public List<RabbitMqPublicationConfiguration> Publications { get; set; } = [];
    } 
    
    public class GatewayConnection 
    {
        public string Name { get; set; } = Environment.MachineName;
        public AmqpUriSpecificationConfiguration? AmpqUri { get; set; } = null;
        public ExchangeConfiguration? Exchange { get; set; }
        public ExchangeConfiguration? DeadLetterExchange { get; set; }
        public ushort Heartbeat { get; set; } = 20;
        public bool PersistMessages { get; set; }
        public ushort ContinuationTimeout { get; set; } = 20;
        
        public RmqMessagingGatewayConnection ToMessagingGatewayConnection()
        {
            return new RmqMessagingGatewayConnection
            {
                Name = Name,
                AmpqUri = AmpqUri != null ? new AmqpUriSpecification(
                    uri: new Uri(AmpqUri.Uri, UriKind.Absolute),
                    connectionRetryCount: AmpqUri.ConnectionRetryCount,
                    retryWaitInMilliseconds: AmpqUri.RetryWaitInMilliseconds,
                    circuitBreakTimeInMilliseconds: AmpqUri.CircuitBreakTimeInMilliseconds) : null,
                Exchange = Exchange != null ? new Exchange(
                    name: Exchange.Name,
                    type: Exchange.Type,
                    durable: Exchange.Durable, 
                    supportDelay: Exchange.SupportDelay) : null,
                DeadLetterExchange = DeadLetterExchange != null ? new Exchange(
                    name: DeadLetterExchange.Name,
                    type: DeadLetterExchange.Type,
                    durable: DeadLetterExchange.Durable, 
                    supportDelay: DeadLetterExchange.SupportDelay) : null,
                Heartbeat = Heartbeat,
                PersistMessages = PersistMessages,
                ContinuationTimeout = ContinuationTimeout,
            };
        }
    }

    public class AmqpUriSpecificationConfiguration
    {
        public string Uri { get; set; } = string.Empty;
        public int ConnectionRetryCount { get; set; } = 3;
        public int RetryWaitInMilliseconds { get; set; } = 1_000;
        public int CircuitBreakTimeInMilliseconds { get; set; } = 60_000;
    }
    
    public class ExchangeConfiguration
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = ExchangeType.Direct;

        public bool Durable { get; set; }

        public bool SupportDelay { get; set; }
    }
    
    public class RabbitMqSubscriptionConfiguration
    {
        
    }
    
    public class RabbitMqPublicationConfiguration
    {
        public string? DataSchema { get; set; }
        public OnMissingChannel MakeChannels { get; set; }
        public string? RequestType { get; set; }
        
        public string Source { get; set; } = "http://goparamore.io";
        
        public string? Subject { get; set; }
        public string? Topic { get; set; }
        public string Type { get; set; } = string.Empty; 
        
        public IDictionary<string, object>? DefaultHeaders { get; set; }
        public IDictionary<string, object>? CloudEventsAdditionalProperties { get; set; }
        
        public string? ReplyTo { get; set; }
        public int WaitForConfirmsTimeOutInMilliseconds { get; set; } = 500;

        public RmqPublication ToPublication()
        {
            Uri? dataschema = null;
            if (!string.IsNullOrEmpty(DataSchema))
            {
                dataschema = new Uri(DataSchema!, UriKind.RelativeOrAbsolute);
            }
            
            RoutingKey? topic = null;
            if (!string.IsNullOrEmpty(Topic))
            {
                topic = new RoutingKey(Topic!);
            }
            
            return new RmqPublication
            {
                DataSchema = dataschema,
                MakeChannels = MakeChannels,
                Source = new Uri(Source, UriKind.RelativeOrAbsolute),
                Subject = Subject,
                Topic = topic,
                Type = new CloudEventsType(Type),
                DefaultHeaders = DefaultHeaders,
                CloudEventsAdditionalProperties = CloudEventsAdditionalProperties,
                ReplyTo = ReplyTo,
                WaitForConfirmsTimeOutInMilliseconds = WaitForConfirmsTimeOutInMilliseconds,
            };
        }
    }
}
