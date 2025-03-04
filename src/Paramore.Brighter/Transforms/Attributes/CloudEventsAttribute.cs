using System;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes;

/// <summary>
/// Support the usage of CloudEvents in the message.
/// We can take the metadata for a CloudEvent from different sources, and they are handled in this order:
/// 1. The <see cref="Publication"/> parameter in the Message Mapper; use this if you want to map them by hand
/// 2. The <see cref="Publication"/> parameter in the CloudEvents transformer; use this if you just want mapping by convention
///     via the <see cref="CloudEventsTransformer"/>
/// 2. The parameters provided to the <see cref="CloudEventsAttribute"/> attribute; use this if you want to hardcode these via the
///     attribute over the <see cref="Publication"/>
/// </summary>
public class CloudEventsAttribute : WrapWithAttribute
{
    /// <inheritdoc />
    /// <summary>
    /// Requests middleware that will set Cloud Events headers. Allows the passing of parameters that will
    /// override any <see cref="Publication"/> properties.
    /// </summary>
    /// <param name="step">The step in the pipeline to apply the middleware</param>
    public CloudEventsAttribute(int step)
        : base(step)
    {

    }

    /// <summary>
    /// Identifies the context in which an event happened.
    /// Often this will include information such as the type of the event source, the organization publishing the
    /// event or the process that produced the event. The exact syntax and semantics behind the data encoded in
    /// the URI is defined by the event producer. 
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// This attribute contains a value describing the type of event related to the originating occurrence.
    /// Often this attribute is used for routing, observability, policy enforcement, etc.
    /// The format of this is producer defined and might include information such as the version of the type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The version of the CloudEvents specification which the event uses.
    /// </summary>
    public string? SpecVersion { get; set; }

    /// <summary>
    /// Content type of data value. This attribute enables data to carry any type of content, whereby format and
    /// encoding might differ from that of the chosen event format. For example, an event rendered using the JSON envelope
    /// format might carry an XML payload in data, and the consumer is informed by this attribute being set to "application/xml".
    /// The rules for how data content is rendered for different datacontenttype values are defined in the event format specifications;
    /// 
    /// For some binary mode protocol bindings, this field is directly mapped to the respective protocol's content-type
    /// metadata property. Normative rules for the binary mode and the content-type metadata mapping can be found in the
    /// respective protocol.
    /// 
    /// In some event formats the datacontenttype attribute MAY be omitted. For example, if a JSON format event has no
    /// datacontenttype attribute, then it is implied that the data is a JSON value conforming to the "application/json"
    /// media type. In other words: a JSON-format event with no datacontenttype is exactly equivalent to one with
    /// datacontenttype="application/json".
    /// 
    /// When translating an event message with no "datacontenttype" attribute to a different format or protocol binding,
    /// the target datacontenttype SHOULD be set explicitly to the implied datacontenttype of the source. 
    /// </summary>
    public string DataContentType { get; set; } = "application/cloudevents";
    
    /// <summary>
    /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be reflected by a different URI.
    /// </summary>
    public string? DataSchema { get; set; }
    
    /// <summary>
    /// This describes the subject of the event in the context of the event producer (identified by source).
    /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
    /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source
    /// context has internal sub-structure.
    /// 
    /// Identifying the subject of the event in context metadata (opposed to only in the data payload) is particularly
    /// helpful in generic subscription filtering scenarios where middleware is unable to interpret the data content.
    /// In the above example, the subscriber might only be interested in blobs with names ending with '.jpg' or '.jpeg'
    /// and the subject attribute allows for constructing a simple and efficient string-suffix filter for that subset
    /// of events. 
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Passes the parameters to the <see cref="CloudEventsTransformer"/> to set the Cloud Events headers
    /// </summary>
    /// <returns></returns>
    public override object?[] InitializerParams()
    {
        return [Source, Type, SpecVersion, DataContentType, DataSchema, Subject];
    }

    /// <summary>
    /// Returns the type of the handler that will be used to process the Cloud Events
    /// </summary>
    /// <returns></returns>
    public override Type GetHandlerType()
    {
        return typeof(CloudEventsTransformer);
    }
}
