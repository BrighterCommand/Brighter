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
/// them from the Brighter <see cref="IRequestContext.Bag"/> if <see cref="JustSayingAttribute.FromContextRequest"/> is enabled.
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
    private string? _type;

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
            _type = subject;
        }

        if (initializerList[3] is string tenant)
        {
            _tenant = tenant;
        }

        if (initializerList[4] is bool fromContextRequest)
        {
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
        message.Header.Bag["Subject"] = Get(_type, nameof(JustSayingAttribute.Type)) ?? publication.Type;

        var tenant = Get(_tenant, nameof(JustSayingAttribute.Tenant)) ?? "all";
        var raisingComponent= Get(_raisingComponent, nameof(JustSayingAttribute.RaisingComponent)) ?? publication.Source.ToString();
        var version = Get(_version, nameof(JustSayingAttribute.Version)) ?? "1.0";
        
        var node = JsonNode.Parse(message.Body.Bytes);
        if (node == null)
        {
            return message;
        }

        var id = node[nameof(IJustSayingRequest.Id)];
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
        node[nameof(IJustSayingRequest.Conversation)] = message.Header.CorrelationId;

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
