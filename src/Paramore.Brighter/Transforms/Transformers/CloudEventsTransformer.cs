using System;
using System.Collections;
using System.Linq;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Transforms.Transformers;

/// <summary>
/// Provides support for the <see href="https://github.com/cloudevents/spec?tab=readme-ov-file">Cloud Events specification</see>
/// by ensuring that our message has the required metadata to support Cloud Events
/// The following Cloud Events attributes are supported:
/// REQUIRED
///     id => the message id <see cref="MessageHeader"/>; you don't set this here, as we use the id from the <see cref="Request"/>
///     source => uses the source Uri from the <see cref="Publication"/> or <see cref="CloudEvents"/> and assigns to the message source <see cref="MessageHeader"/>
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
    private string? _datacontenttype;
    private Uri? _dataschema;
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

    public void InitializeWrapFromAttributeParams(params object[] initializerList)
    {
        
        _source = initializerList.ElementAtOrDefault(0) == null ? null : (Uri) initializerList.ElementAtOrDefault(0)!; 
        _type = initializerList.ElementAtOrDefault(1)?.ToString(); 
        _datacontenttype = initializerList.ElementAtOrDefault(2)?.ToString();
        _dataschema = initializerList.ElementAtOrDefault(3) == null ? null : (Uri)initializerList.ElementAtOrDefault(3)!; 
        _subject = initializerList.ElementAtOrDefault(4)?.ToString();
    }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
    {
        throw new System.NotImplementedException();
    }

    public Message Wrap(Message message, Publication publication)
    {
        message.Header.Source = _source ?? publication.Source;
        message.Header.Type = _type ?? publication.Type;
        message.Header.ContentType = _datacontenttype ?? publication.ContentType;
        message.Header.DataSchema = _dataschema ?? publication.DataSchema;
        message.Header.Subject = _subject ?? publication.Subject;
        return message;
    }

    public Message Unwrap(Message message)
    {
        throw new System.NotImplementedException();
    }
}
