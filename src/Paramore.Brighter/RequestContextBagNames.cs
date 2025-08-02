using System.Collections.Generic;

namespace Paramore.Brighter;

/// <summary>
/// Contains well-known keys used for accessing values in Brighter's internal request context bag.
/// </summary>
/// <remarks>
/// These constants represent reserved names used within Brighter's message processing pipeline to store
/// and retrieve specific contextual information during message handling.
/// </remarks>
public static class RequestContextBagNames
{
  
    /// <summary>
    /// Key used to store additional extension properties for CloudEvents in the request context bag.
    /// </summary>
    /// <remarks>
    /// This reserved name is used by Brighter's default CloudEvent mapper to handle custom CloudEvent extensions.
    /// <para>
    /// <strong>Important:</strong> The value associated with this key in the context bag must be of type
    /// <see cref="Dictionary{TKey,TValue}"/> where TKey is <see cref="string"/> and TValue is <see cref="object"/>.
    /// </para>
    /// <para>
    /// Additional properties can be provided through:
    /// <list type="bullet">
    /// <item><description>Programmatic configuration of the message mapper</description></item>
    /// <item><description>Declarative <c>[CloudEvent]</c> attributes applied to message types</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// These properties will be serialized as CloudEvent extensions in the generated message envelope.
    /// </para>
    /// <example>
    /// Setting additional properties via context bag:
    /// <code>
    /// var additionalProps = new Dictionary&lt;string, object&gt;
    /// {
    ///     ["myextension"] = "value",
    ///     ["numericExtension"] = 42
    /// };
    /// 
    /// requestContext.Bag[RequestContextBagNames.CloudEventsAdditionalProperties] = additionalProps;
    /// </code>
    /// </example>
    /// </remarks>
    public const string CloudEventsAdditionalProperties = "Brighter-CloudEvents-AdditionalProperties";
    
    
    /// <summary>
    /// Key used to store the job ID, representing an instance of a workflow, in the request context bag.
    /// </summary>
    /// <remarks>Reserved for future usage</remarks>
    public const string JobId = "Brighter-JobId";
    
    /// <summary>
    /// Key used to specify a custom partition key override in the request context bag.
    /// </summary>
    /// <remarks>
    /// This reserved name allows setting a custom partition key for message routing.
    /// <para>
    /// The value associated with this key can be either:
    /// <list type="bullet">
    /// <item><description>A <see cref="string"/> value representing the partition key</description></item>
    /// <item><description>A <see cref="Paramore.Brighter.PartitionKey"/> value</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Important notes:</strong>
    /// <list type="bullet">
    /// <item><description>This value is optional and may be ignored by the transport if partitioning isn't supported</description></item>
    /// <item><description><strong>Custom message mappers may choose to ignore this value entirely</strong></description></item>
    /// <item><description>When not set, the transport's default partitioning strategy will be used</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Setting a string partition key:
    /// <code>
    /// requestContext.Bag[RequestContextBagNames.PartitionKey] = "customer-1234";
    /// </code>
    /// 
    /// Setting a Brighter partition key:
    /// <code>
    /// requestContext.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("customer-1234");
    /// </code>
    /// </example>
    /// <para>
    /// Default Brighter mappers will use this value when present.
    /// </para>
    /// </remarks>
    public const string PartitionKey = "Brighter-PartitionKey";
    
    /// <summary>
    /// Key used to dynamically set or override message headers via the request context bag.
    /// </summary>
    /// <remarks>
    /// This reserved name allows runtime configuration of message headers during publication.
    /// <para>
    /// <strong>Value must be:</strong> An <see cref="IDictionary{TKey, TValue}"/> where TKey is <see cref="string"/> 
    /// and TValue is <see cref="object"/> representing header name-value pairs.
    /// </para>
    /// <para>
    /// <strong>Important notes:</strong>
    /// <list type="bullet">
    /// <item><description>Headers set here take precedence over static header configurations</description></item>
    /// <item><description><strong>Custom message mappers may ignore these headers entirely</strong></description></item>
    /// <item><description>Merged with <see cref="Publication.DefaultHeaders"/> by default Brighter mappers</description></item>
    /// </list>
    /// </para>
    /// <example>
    /// Setting dynamic headers:
    /// <code>
    /// requestContext.Bag[RequestContextBagNames.Headers] = new Dictionary&lt;string, object&gt;
    /// {
    ///     ["x-custom-header"] = "runtime-value",
    ///     ["x-timestamp"] = DateTime.UtcNow,
    /// };
    /// </code>
    /// </example>
    /// <para>
    /// Default Brighter message mappers will apply these headers after any static headers,
    /// allowing runtime values to override configuration. Custom mappers may implement
    /// different merging strategies or ignore this context completely.
    /// </para>
    /// </remarks>
    public const string Headers = "Brighter-Headers";

    /// <summary>
    /// Used to store the worfkflow ID, which indicates which workflow this request belongs to, in the request context bag.
    /// </summary>
    /// <remarks>Reserved for future usage</remarks>
    public const string WorkflowId = "Brighter-WorkflowId";
    
}
