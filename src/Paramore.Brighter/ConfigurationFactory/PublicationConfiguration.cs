using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.ConfigurationFactory;

/// <summary>
/// Base class for publication configuration that can be loaded from configuration sources.
/// Provides properties for configuring how Brighter publishes messages to a transport,
/// including routing, message metadata, and CloudEvents properties.
/// </summary>
/// <remarks>
/// This abstract class is intended to be extended by transport-specific publication configuration classes
/// (e.g., RabbitMQ, Kafka, AWS SNS) that bind to configuration sections. It provides common publication
/// properties and helper methods for deriving runtime values from configuration.
/// Properties mirror those in <see cref="Publication"/> but use configuration-friendly types (e.g., string instead of Uri).
/// See ADR 0035 for details on Brighter's configuration support strategy.
/// </remarks>
public abstract class PublicationConfiguration
{
    /// <summary>
    /// Gets or sets the URI identifying the schema that data adheres to.
    /// </summary>
    /// <value>The schema URI as a <see cref="string"/>, or null if not specified.</value>
    /// <remarks>
    /// From the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">CloudEvents Specification</see>.
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be reflected by a different URI.
    /// This is an optional CloudEvents attribute.
    /// </remarks>
    public string? DataSchema { get; set; }
    
    /// <summary>
    /// Gets or sets the behavior for creating missing channels.
    /// </summary>
    /// <value>An <see cref="OnMissingChannel"/> value indicating how to handle missing infrastructure.</value>
    /// <remarks>
    /// Controls whether Brighter should create infrastructure (topics, exchanges) if it doesn't exist.
    /// Use <see cref="OnMissingChannel.Create"/> for development environments and <see cref="OnMissingChannel.Validate"/>
    /// or <see cref="OnMissingChannel.Assume"/> for production where infrastructure is managed separately.
    /// </remarks>
    public OnMissingChannel MakeChannels { get; set; }
    
    /// <summary>
    /// Gets or sets the fully qualified type name of the request/message type for this publication.
    /// </summary>
    /// <value>The fully qualified type name as a <see cref="string"/>, or null for untyped publications.</value>
    /// <remarks>
    /// This should be the full type name including namespace (e.g., "MyApp.Commands.ProcessOrderCommand").
    /// The type is resolved at runtime using reflection across all loaded assemblies via <see cref="GetRequestType"/>.
    /// Used to set the <see cref="Publication.RequestType"/> property when building publication objects.
    /// </remarks>
    public string? RequestType { get; set; }
    
    /// <summary>
    /// Gets or sets the source URI identifying the context in which an event happened.
    /// </summary>
    /// <value>The source URI as a <see cref="string"/>. Default is "http://goparamore.io".</value>
    /// <remarks>
    /// From the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">CloudEvents Specification</see>.
    /// Identifies the context in which an event happened. Often this includes information such as the type of
    /// the event source, the organization publishing the event, or the process that produced the event.
    /// Producers MUST ensure that source + id is unique for each distinct event.
    /// This is a required CloudEvents attribute.
    /// </remarks>
    public string Source { get; set; } = "http://goparamore.io";
    
    /// <summary>
    /// Gets or sets the subject of the event in the context of the event producer.
    /// </summary>
    /// <value>The subject as a <see cref="string"/>, or null if not specified.</value>
    /// <remarks>
    /// From the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">CloudEvents Specification</see>.
    /// Describes the subject of the event in the context of the event producer (identified by <see cref="Source"/>).
    /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
    /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the
    /// source context has internal sub-structure.
    /// This is an optional CloudEvents attribute.
    /// </remarks>
    public string? Subject { get; set; }
    
    /// <summary>
    /// Gets or sets the topic (routing key) for this publication.
    /// </summary>
    /// <value>The topic as a <see cref="string"/>, or null if not specified.</value>
    /// <remarks>
    /// In a pub-sub scenario, this is typically the topic to which messages are published. Subscribers then
    /// create their own queues which the broker delivers messages to based on routing rules.
    /// For topic-based transports like RabbitMQ, this supports wildcards (* and #).
    /// Maps to the <see cref="Publication.Topic"/> property when building publication objects.
    /// </remarks>
    public string? Topic { get; set; }
    
    /// <summary>
    /// Gets or sets the CloudEvents type describing the type of event.
    /// </summary>
    /// <value>The event type as a <see cref="string"/>. Default is an empty string.</value>
    /// <remarks>
    /// From the <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">CloudEvents Specification</see>.
    /// This attribute contains a value describing the type of event related to the originating occurrence.
    /// Often this attribute is used for routing, observability, policy enforcement, etc.
    /// SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines
    /// the semantics of this event type.
    /// This is a required CloudEvents attribute.
    /// </remarks>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the default headers to be included in published messages.
    /// </summary>
    /// <value>A dictionary of header name-value pairs, or null if no default headers are specified.</value>
    /// <remarks>
    /// These headers will be automatically added to all messages published through Brighter's message producers
    /// when using default message mappers. Headers should be structured as key-value pairs where the key is the
    /// header name (string) and the value is the header value (object).
    /// <para>
    /// Example configuration:
    /// <code>
    /// "DefaultHeaders": {
    ///     "x-correlation-id": "00000000-0000-0000-0000-000000000000",
    ///     "x-message-type": "MyApp.Events.OrderCreated"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Note: These headers are only applied when using Brighter's default message mapping pipeline.
    /// Custom mappers may ignore this property.
    /// </para>
    /// </remarks>
    public Dictionary<string, object>? DefaultHeaders { get; set; }
    
    /// <summary>
    /// Gets or sets additional CloudEvents properties beyond the standard attributes.
    /// </summary>
    /// <value>A dictionary of additional CloudEvents property name-value pairs, or null if no additional properties are specified.</value>
    /// <remarks>
    /// This enables the inclusion of custom or vendor-specific metadata beyond the standard CloudEvents attributes
    /// (id, source, type, etc.). These properties are serialized alongside core CloudEvents attributes when mapping
    /// to a CloudEvent message.
    /// <para>
    /// Use this dictionary to attach any non-standard CloudEvents attributes or extensions pertinent to your
    /// application or integration requirements. During serialization to CloudEvent JSON, the key-value pairs
    /// are added as top-level properties in the resulting JSON.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> If any key in this dictionary conflicts with a standard CloudEvents property
    /// (e.g., "id", "source", "type"), the value in this dictionary will override the standard property during
    /// serialization. Exercise caution to avoid unintended overwrites.
    /// </para>
    /// <para>
    /// This property is utilized by <c>CloudEventJsonMessageMapper</c> and <c>CloudEventsTransformer</c>.
    /// </para>
    /// </remarks>
    public Dictionary<string, object>? CloudEventsAdditionalProperties { get; set; }
    
    /// <summary>
    /// Gets or sets the reply-to topic for request-reply messaging patterns.
    /// </summary>
    /// <value>The reply-to topic as a <see cref="string"/>, or null if not using request-reply.</value>
    /// <remarks>
    /// Used when doing Request-Reply instead of Publish-Subscribe to identify the queue or topic that the
    /// sender is listening on for responses. Usually a sender listens on a private queue so they do not
    /// have to filter replies intended for other listeners.
    /// This is an optional property only needed for request-reply scenarios.
    /// </remarks>
    public string? ReplyTo { get; set; }
    
    /// <summary>
    /// Gets the request type by resolving the <see cref="RequestType"/> string to a <see cref="Type"/> using reflection.
    /// </summary>
    /// <returns>The resolved <see cref="Type"/>, or null if <see cref="RequestType"/> is not specified.</returns>
    /// <exception cref="ConfigurationException">Thrown when <see cref="RequestType"/> is specified but the type cannot be found in any loaded assembly.</exception>
    /// <remarks>
    /// This method searches all loaded assemblies in the current <see cref="AppDomain"/> for a concrete, non-abstract
    /// class matching the fully qualified type name specified in <see cref="RequestType"/>.
    /// The type must be a class (not an interface or struct) and must not be abstract.
    /// <para>
    /// Example: "MyApp.Commands.ProcessOrderCommand" or "MyApp.Commands.ProcessOrderCommand, MyApp" if the assembly name is needed.
    /// </para>
    /// <para>
    /// This method is typically called by derived classes when building <see cref="Publication"/> objects.
    /// </para>
    /// </remarks>
    protected Type? GetRequestType()
    {
        if (string.IsNullOrEmpty(RequestType))
        {
            return null;
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            var type = types.FirstOrDefault(x => x.FullName == RequestType);
            if (type != null && type.IsClass && !type.IsAbstract)
            {
                return type;
            }
        }

        throw new ConfigurationException(
            $"RequestType '{RequestType}' could not be resolved to a valid type. " +
            $"Ensure the type name is fully qualified (e.g., 'MyApp.Commands.ProcessOrderCommand'), " +
            $"the assembly containing the type is loaded, and the type is a concrete class (not abstract or an interface). " +
            $"Searched {assemblies.Length} loaded assemblies.");
    }    
}
