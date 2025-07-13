using System;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Base class for JustSaying-compatible commands in Brighter framework.
/// Provides infrastructure properties required for interoperability with JustSaying library.
/// </summary>
/// <remarks>
/// This class extends Brighter's Event with additional metadata properties needed for
/// JustSaying message headers. When commands inherit from this class, they automatically
/// support the optimized mapping path in JustSayingMessageMapper, avoiding slower generic JSON manipulation.
/// 
/// Key features:
/// - Implements IJustSayingRequest for direct header mapping
/// - Provides standard JustSaying message properties (timestamp, version, tenant, etc.)
/// - Enables efficient serialization/deserialization through strongly-typed properties
/// 
/// Derived classes should use the base constructors to ensure proper message ID handling.
/// </remarks>
public class JustSayingEvent : Event, IJustSayingRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingEvent"/> class. 
    /// </summary>
#if NET9_0_OR_GREATER
    public JustSayingEvent() : this(Guid.CreateVersion7())
#else
    public JustSayingEvent() : this(Guid.NewGuid())
#endif
    {
        
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingEvent"/> class. 
    /// </summary>
    /// <param name="id">The identifier</param>
    public JustSayingEvent(Id id) : base(id)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingEvent"/> class. 
    /// </summary>
    /// <param name="id">The identifier</param>
    public JustSayingEvent(Guid id) : base(id)
    {
    }

    /// <inheritdoc />
    public DateTimeOffset TimeStamp { get; set; }
    
    /// <inheritdoc />
    public string? RaisingComponent { get; set; }
    
    /// <inheritdoc />
    public string? Version { get; set; }
    
    /// <inheritdoc />
    public string? SourceIp { get; set; }
    
    /// <inheritdoc />
    public string? Tenant { get; set; }
    
    /// <inheritdoc />
    public Id? Conversation { get; set; }
}
