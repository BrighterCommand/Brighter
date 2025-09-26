using System;
using System.Buffers;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Configuration for publishing messages to Apache Pulsar within the Brighter framework
/// </summary>
/// <remarks>
/// Extends Brighter's base Publication with Pulsar-specific producer configuration options.
/// Used to define producer behavior, schema handling, and message formatting.
/// </remarks>
public class PulsarPublication : Publication
{
    /// <summary>
    /// Gets or sets the compression type for published messages
    /// </summary>
    /// <value>
    /// Default: CompressionType.None
    /// </value>
    public CompressionType CompressionType { get; set; } = CompressionType.None;
    
    /// <summary>
    /// Gets or sets the optional producer name (used for diagnostics)
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the schema version identifier
    /// </summary>
    public byte[]? SchemaVersion { get; set; }
    
    /// <summary>
    /// Gets or sets the initial sequence ID for messages
    /// </summary>
    public ulong InitialSequenceId { get; set; }
    
    /// <summary>
    /// Gets or sets the producer access mode
    /// </summary>
    /// <value>
    /// Default: ProducerAccessMode.Shared
    /// </value>
    public ProducerAccessMode AccessMode { get; set; } = ProducerAccessMode.Shared;

    /// <summary>
    /// Gets or sets the schema for message serialization
    /// </summary>
    /// <value>
    /// Default: ByteSequence schema (raw byte array)
    /// </value>
    public ISchema<ReadOnlySequence<byte>> Schema { get; set; } = DotPulsar.Schema.ByteSequence; 

    /// <summary>
    /// Gets or sets the function to generate message sequence IDs
    /// </summary>
    /// <remarks>
    /// Default behavior returns 0 (no sequence tracking).
    /// Override to implement custom sequence generation.
    /// </remarks>
    public Func<Message, ulong> GenerateSequenceId { get; set; } = _ => 0;
    
    /// <summary>
    /// Gets or sets the time provider for delayed message scheduling
    /// </summary>
    /// <value>
    /// Default: System time provider
    /// </value>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// Gets or sets instrumentation options for monitoring
    /// </summary>
    public InstrumentationOptions? Instrumentation { get; set; }
    
    /// <summary>
    /// Gets or sets an optional configuration callback for advanced producer setup
    /// </summary>
    /// <remarks>
    /// Allows direct access to the Pulsar producer builder for custom configuration
    /// not exposed through standard properties.
    /// </remarks>
    public Action<IProducerBuilder<ReadOnlySequence<byte>>>? Configure { get; set; }
}

/// <summary>
/// Typed publication configuration for specific message types
/// </summary>
/// <typeparam name="T">The request type being published</typeparam>
/// <remarks>
/// Specializes <see cref="PulsarPublication"/> for specific message types.
/// Automatically sets the RequestType property to the specified generic type.
/// </remarks>
public class PulsarPublication<T> : PulsarPublication
    where T : IRequest
{
    /// <summary>
    /// Initializes a new instance of the typed publication
    /// </summary>
    /// <remarks>
    /// Sets the RequestType property to the generic type argument
    /// </remarks>
    public PulsarPublication()
    {
        RequestType = typeof(T);
    }
}
