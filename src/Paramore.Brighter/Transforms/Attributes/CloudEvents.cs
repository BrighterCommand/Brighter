using System;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Transforms.Attributes;

/// <summary>
/// Support the usage of CloudEvents in the message.
/// We can take the metadata for a CloudEvent from different sources, and they are handled in this order:
/// 1. The <see cref="Publication"/> parameter in the Message Mapper; use this if you want to map them by hand
/// 2. The <see cref="Publication"/> parameter in the CloudEvents transformer; use this if you just want mapping by convention
///     via the <see cref="CloudEventsTransformer"/>
/// 2. The parameters provided to the <see cref="CloudEvents"/> attribute; use this if you want to hardcode these via the
///     attribute over the <see cref="Publication"/>
/// </summary>
public class CloudEvents : WrapWithAttribute
{
    private readonly string _source;
    private readonly string _type;
    private readonly string _contentType;
    private readonly Uri _dataSchema;
    private readonly string _subject;

    /// <inheritdoc />
    /// <summary>
    /// Requests middleware that will set Cloud Events headers. Allows the passing of parameters that will
    /// override any <see cref="Publication"/> properties.
    /// </summary>
    /// <param name="step">The step in the pipeline to apply the middleware</param>
    /// <param name="source">Identifies the context in which an event happened; often a URI identifying the producer</param>
    /// <param name="type">The type of event; SHOULD be prefixed with a reverse-DNS name </param>
    /// <param name="contentType">The type of the payload of the message, defaults to tex/plain</param>
    /// <param name="dataSchema">A Uri that identifies the schema that data adheres to</param>
    /// <param name="subject">Describes the subject of the event in the context of the event producer</param>
    public CloudEvents(int step, string source, string type, string contentType, Uri dataSchema, string subject) 
        : base(step)
    {
        _source = source;
        _type = type;
        _contentType = contentType;
        _dataSchema = dataSchema;
        _subject = subject;
    }

    /// <summary>
    /// Passes the parameters to the <see cref="CloudEventsTransformer"/> to set the Cloud Events headers
    /// </summary>
    /// <returns></returns>
    public override object[] InitializerParams()
    {
        return [_source, _type, _contentType, _dataSchema, _subject];
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
