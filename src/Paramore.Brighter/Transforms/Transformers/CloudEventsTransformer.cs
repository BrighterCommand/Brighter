using System;
using System.Net.Mime;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Transforms.Transformers;

/// <summary>
/// Provides support for the <see href="https://github.com/cloudevents/spec?tab=readme-ov-file">Cloud Events specification</see>
/// by ensuring that our message has the required metadata to support Cloud Events
/// The following Cloud Events attributes are supported:
/// REQUIRED
///     id => the message id <see cref="MessageHeader"/>; you don't set this here, as we use the id from the <see cref="Request"/>
///     source => uses the source Uri from the <see cref="Publication"/> or <see cref="CloudEventsAttribute"/> and assigns to the message source <see cref="MessageHeader"/>
///     specversion => uses the spec version <see cref="MessageHeader"/>; you don't set this and it defaults to 1.0
///     type => uses the type <see cref="MessageHeader"/>; as we used type based routing, we recommend using the hostname
///         scoped name of the request class you are sending
/// OPTIONAL
///      datacontenttype => sets the content type for <see cref="MessageBody"/> and <see cref="MessageHeader"/>
///      dataschema => sets the schema for <see cref="MessageBody"/> and <see cref="MessageHeader"/>
///      subject => sets the subject for <see cref="MessageHeader"/>
///      time => sets the timestamp for <see cref="MessageHeader"/>
/// </summary>
public class CloudEventsTransformer : IAmAMessageTransform
{
    private Uri? _source;
    private string? _type;
    private string? _specVersion;
    private ContentType? _dataContentType;
    private Uri? _dataSchema;
    private string? _subject;

    /// <summary>
    /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
    /// </summary>
    /// <value>The context.</value>
    public IRequestContext? Context { get; set; }

    public void Dispose()
    {
        //no op as we have no unmanaged resources
    }

    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList[0] is string source)
        {
            _source = new Uri(source, UriKind.RelativeOrAbsolute);
        }

        if (initializerList[1] is string type)
        {
            _type = type;
        }

        if (initializerList[2] is string specVersion)
        {
            _specVersion = specVersion;
        }

        if (initializerList[3] is string dataContentType)
        {
            _dataContentType = new ContentType(dataContentType);
        }

        if (initializerList[4] is string dataSchema)
        {
            _dataSchema = new Uri(dataSchema, UriKind.RelativeOrAbsolute);
        }

        if (initializerList[5] is string subject)
        {
            _subject = subject;
        }
    }

    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }

    public Message Wrap(Message message, Publication publication)
    {
        message.Header.Source = _source ?? publication.Source;
        message.Header.Type = _type ?? publication.Type;
        message.Header.DataSchema = _dataSchema ?? publication.DataSchema;
        message.Header.Subject = _subject ?? publication.Subject;
        message.Header.ContentType = _dataContentType ?? publication.ContentType;
        message.Header.SpecVersion = _specVersion ?? message.Header.SpecVersion;
        return message;
    }

    public Message Unwrap(Message message)
        => message;
}
