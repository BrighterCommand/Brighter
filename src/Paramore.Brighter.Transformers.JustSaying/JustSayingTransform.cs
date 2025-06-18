using System.Text.Json;
using System.Text.Json.Nodes;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Implements a Brighter message transform that makes messages compatible with JustSaying's
/// message envelope format. This transform enriches or extracts specific metadata fields
/// (like RaisingComponent, Version, Tenant, and Subject) that JustSaying expects in its messages.
/// </summary>
/// <remarks>
/// This transformer is typically used in conjunction with the <see cref="JustSayingAttribute"/>
/// to automatically apply the necessary metadata during message publication (Wrap) or
/// consumption (Unwrap). It populates a JSON message body with these fields, allowing
/// JustSaying to correctly route, version, and filter messages.
/// <para>
/// During the <see cref="Wrap"/> operation, if the message body is a valid JSON,
/// this transform injects the configured properties directly into the JSON payload.
/// If no specific values are provided via the attribute, it can attempt to retrieve
/// them from the Brighter <see cref="IRequestContext.Bag"/>.
/// </para>
/// <para>
/// The <see cref="Unwrap"/> operation is intentionally a no-op as JustSaying's
/// deserialization often handles the entire message structure, and Brighter's
/// core deserialization handles the body.
/// </para>
/// </remarks>
public class JustSayingTransform : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private string? _raisingComponent;
    private string? _version;
    private string? _tenant;
    private string? _subject;

    /// <inheritdoc cref="IAmAMessageTransform.Context"/>
    public IRequestContext? Context { get; set; }
    
    /// <inheritdoc cref="IAmAMessageTransform.InitializeWrapFromAttributeParams"/>
    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList.Length != 4)
        {
            return;
        }

        if (initializerList[0] is string raisingComponent)
        {
            _raisingComponent = raisingComponent;
        }

        if (initializerList[1] is string version)
        {
            _version = version;
        }

        if (initializerList[2] is string subject)
        {
            _subject = subject;
        }

        if (initializerList[3] is string tenant)
        {
            _tenant = tenant;
        }
    }

    /// <inheritdoc cref="IAmAMessageTransform.InitializeUnwrapFromAttributeParams"/>
    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }
    
    /// <inheritdoc />
    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
    {
        return Task.FromResult(Wrap(message, publication));
    }

    /// <inheritdoc />
    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.FromResult(Unwrap(message));
    }

    /// <inheritdoc />
    public Message Wrap(Message message, Publication publication)
    {
        message.Header.Subject = GetSubject(message, publication);

        var tenant = GetTenant();
        var raisingComponent = GetRaisingComponent(message, publication);
        var version = GetVersion();
        
        var node = JsonNode.Parse(message.Body.Bytes);
        if (node == null)
        {
            return message;
        }

        var id = node[nameof(IJustSayingRequest.Id)] ?? node[nameof(IJustSayingRequest.Id).ToLower()];
        if (id == null 
            || id.GetValueKind() != JsonValueKind.String 
            || !Guid.TryParse(id.GetValue<string>(), out _))
        {
            node[nameof(IJustSayingRequest.Id)] = Guid.NewGuid().ToString();
        } 
        
        node[nameof(IJustSayingRequest.Tenant)] = tenant;
        node[nameof(IJustSayingRequest.Version)] = version;
        node[nameof(IJustSayingRequest.TimeStamp)] = message.Header.TimeStamp;
        node[nameof(IJustSayingRequest.RaisingComponent)] = raisingComponent;
        node[nameof(IJustSayingRequest.Conversation)] = message.Header.CorrelationId.Value;

        message.Body = new MessageBody(node.ToJsonString());
        return message;
    }

    /// <inheritdoc />
    public Message Unwrap(Message message)
    {
        return message;
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
    }

    private string GetSubject(Message message, Publication publication)
    {
        if (!string.IsNullOrEmpty(_subject))
        {
            return _subject;
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            return message.Header.Subject;
        }

        if (!string.IsNullOrEmpty(publication.Subject))
        {
            return publication.Subject;
        }

        if (publication.RequestType != null)
        {
            return publication.RequestType.Name;
        }

        throw new InvalidOperationException("No Subject was define via Attribute/Message/SnsPublication");
    }

    private string? GetTenant()
    {
        if (!string.IsNullOrEmpty(_tenant))
        {
            return _tenant;
        }

        if (Context != null 
            && Context.Bag.TryGetValue(RequestContextAttributesName.Tenant, out var obj) 
            && obj is string tenant && !string.IsNullOrEmpty(tenant))
        {
            return tenant;
        }

        return null;
    }

    private string GetRaisingComponent(Message message, Publication publication)
    {
        if (!string.IsNullOrEmpty(_raisingComponent))
        {
            return _raisingComponent;
        }

        if (Context != null
            && Context.Bag.TryGetValue(RequestContextAttributesName.RasingComponent, out var obj)
            && obj is string component && !string.IsNullOrEmpty(component))
        {
            return component;
        }

        if (message.Header.Source.ToString() != MessageHeader.DefaultSource)
        {
            return message.Header.Source.ToString();
        }

        return publication.Source.ToString();
    }
    
    private string GetVersion()
    {
        if (!string.IsNullOrEmpty(_version))
        {
            return _version;
        }

        if (Context != null
            && Context.Bag.TryGetValue(RequestContextAttributesName.Version, out var obj)
            && obj is string version && !string.IsNullOrEmpty(version))
        {
            return version;
        }

        return "1.0.0";
    }

    private string? Get(string? attributeValue, string propertyName)
    {
        if (!string.IsNullOrEmpty(attributeValue))
        {
            return attributeValue;
        }
        
        if (Context != null && Context.Bag.TryGetValue(propertyName, out var propertyValue))
        {
            return (string)propertyValue;
        }
        
        return null;
    }
}
