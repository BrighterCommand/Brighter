using System;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Represents a connection gateway to Apache Pulsar, providing configuration and client creation capabilities.
/// Manages the lifecycle of the underlying Pulsar client using the Singleton pattern.
/// </summary>
/// <remarks>
/// This class is thread-safe and intended to be shared across consumers/producers in an application.
/// The created Pulsar client is cached after initial creation.
/// </remarks>
public class PulsarMessagingGatewayConnection
{
    /// <summary>
    /// Gets or sets the optional producer name identifier (used for diagnostics and monitoring)
    /// </summary>
    public string? ProducerName { get; set; }
    
    /// <summary>
    /// Gets or sets the Apache Pulsar service URL (e.g., "pulsar://localhost:6650")
    /// </summary>
    /// <remarks>
    /// Required if not provided through the Configuration callback
    /// </remarks>
    public Uri? ServiceUrl { get; set; }
    
    /// <summary>
    /// Gets or sets an optional configuration callback for advanced Pulsar client settings
    /// </summary>
    /// <remarks>
    /// Allows customization of the Pulsar client builder beyond basic properties.
    /// Invoked during client creation before the client is built.
    /// </remarks>
    public Action<IPulsarClientBuilder>? Configuration { get; set; }

    /// <summary>
    /// Gets or sets the instrumentation options controlling what metrics are collected
    /// </summary>
    /// <value>
    /// Default: InstrumentationOptions.All (collect all available metrics)
    /// </value>
    public InstrumentationOptions Instrumentation { get; set; } = InstrumentationOptions.All;
    
    private IPulsarClient? _pulsarClient;
    
    public IPulsarClient Create()
    {
        if (_pulsarClient != null)
        {
            return _pulsarClient;
        }
        
        var builder = PulsarClient.Builder();

        if (ServiceUrl != null)
        {
            builder.ServiceUrl(ServiceUrl);
        }
        
        Configuration?.Invoke(builder);
        
        return _pulsarClient = builder.Build();
    }
}
