using System;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Base class for JustSaying-compatible commands in Brighter framework.
/// Provides infrastructure properties required for interoperability with JustSaying library.
/// </summary>
/// <remarks>
/// This class extends Brighter's Command with additional metadata properties needed for
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
public class JustSayingCommand : Command, IJustSayingRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingCommand"/> class. 
    /// </summary>
#if NET9_0_OR_GREATER
    public JustSayingCommand() : this(Guid.CreateVersion7())
#else
    public JustSayingCommand() : this(Guid.NewGuid())
#endif
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingCommand"/> class. 
    /// </summary>
    /// <param name="id">The identifier</param>
    public JustSayingCommand(Id id) : base(id)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingCommand"/> class. 
    /// </summary>
    /// <param name="id">The identifier</param>
    public JustSayingCommand(Guid id) : base(id)
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
