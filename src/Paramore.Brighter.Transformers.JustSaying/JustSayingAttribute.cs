using System;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// An attribute that makes a Brighter message compatible with JustSaying by
/// applying the <see cref="JustSayingTransform"/> during message serialization
/// or deserialization. This allows Brighter commands/events to be published to
/// or consumed from JustSaying-managed message queues (e.g., AWS SQS/SNS).
/// </summary>
/// <remarks>
/// This attribute works by injecting a transformation step into the Brighter pipeline.
/// It provides properties that correspond to common JustSaying message envelope fields,
/// enabling Brighter messages to be properly formatted for JustSaying and vice-versa.
/// <para>
/// When applied to a Brighter command or event, the <see cref="JustSayingTransform"/>
/// (specified by <see cref="GetHandlerType"/>) will use the values provided by this attribute
/// to populate or read message metadata relevant to JustSaying's message routing and versioning.
/// </para>
/// <para>
/// The <see cref="FromContextRequest"/> property determines how some metadata fields
/// are sourced during the transformation.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)] 
public class JustSayingAttribute : WrapWithAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JustSayingAttribute"/> class.
    /// </summary>
    /// <param name="step">
    /// The step order in the message transformation pipeline. Lower numbers execute earlier.
    /// </param>
    public JustSayingAttribute(int step) : base(step)
    {
    }
    
    /// <summary>
    /// Gets or sets the name of the component raising the message. This often corresponds
    /// to the service or application that publishes the message.
    /// </summary>
    public string? RaisingComponent { get; set; }
    
    /// <summary>
    /// Gets or sets the version of the message schema or the publishing component.
    /// This is crucial for versioning strategies in JustSaying.
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// Gets or sets the tenant identifier associated with the message.
    /// Useful for multi-tenant applications to route messages by tenant.
    /// </summary> 
    public string? Tenant { get; set; }
    
    /// <summary>
    /// Gets or sets the message's subject. In JustSaying, this can sometimes be
    /// used to derive routing keys or topic names.
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// Get or sets the case-sensitive during deserialization the payload to fulfil
    /// the missing JustSaying message property
    /// </summary>
    public bool PropertyCaseSensitive { get; set; }

    /// <inheritdoc />
    public override object?[] InitializerParams()
    {
        return [RaisingComponent, Version, Type, Tenant, PropertyCaseSensitive];
    }

    /// <inheritdoc />
    public override Type GetHandlerType()
    {
        return typeof(JustSayingTransform);
    }
}
