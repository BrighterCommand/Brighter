using System;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// A message transform that adds JustSaying-compatible metadata to Brighter messages for interoperability.
/// </summary>
/// <remarks>
/// <para>
/// This transform enables compatibility between Brighter and JustSaying by enriching message headers and bodies 
/// with JustSaying-specific properties (e.g., timestamp, version, tenant) during message publication. 
/// It works with any message type through JSON node manipulation, allowing interoperability without requiring 
/// messages to implement <see cref="IJustSayingRequest"/> or inherit from <see cref="JustSayingCommand"/>.
/// </para>
/// <para>
/// <strong>Performance Note:</strong> For optimal performance, prefer using <see cref="JustSayingMessageMapper{TMessage}"/> 
/// with messages that implement <see cref="IJustSayingRequest"/> or inherit from <see cref="JustSayingCommand"/> or /<see cref="JustSayingEvent"/>. 
/// This transform uses dynamic JSON manipulation, which has higher overhead compared to the strongly-typed mapping in <see cref="JustSayingMessageMapper{TMessage}"/>.
/// </para>
/// <para>
/// Configuration:
/// - Apply this transform via the <see cref="IAmAMessageMapper{TMessage}"/> attribute on <see cref="IAmAMessageMapper{TMessage}.MapToRequest"/> 
/// - Parameters like <c>raisingComponent</c>, <c>version</c>, and <c>tenant</c> can be set through attribute constructor arguments
/// - Automatically populates missing metadata from message context or defaults
/// </para>
/// </remarks>
public class JustSayingTransform : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private string? _raisingComponent;
    private string? _version;
    private string? _tenant;
    private string? _subject;
    private bool _caseSensitive;

    /// <inheritdoc cref="IAmAMessageTransform.Context"/>
    public IRequestContext? Context { get; set; }
    
    /// <inheritdoc cref="IAmAMessageTransform.InitializeWrapFromAttributeParams"/>
    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
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

        if (initializerList[4] is bool caseSensitive)
        {
            _caseSensitive = caseSensitive;
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
        try
        {
            var node = JsonNode.Parse(message.Body.Bytes, 
                new JsonNodeOptions { PropertyNameCaseInsensitive = !_caseSensitive }, 
                new JsonDocumentOptions { MaxDepth = 0 });
        
            if (node == null)
            {
                return message;
            }

            SetId(node, message);
            SetTimeStamp(node, message);
            SetConversation(node, message);
            SetRaisingComponent(node, message, publication);
            SetTenant(node);
            SetVersion(node);
        
            message.Header.ContentType = new ContentType("application/json");
            message.Header.Subject = GetSubject(message, publication);

            message.Body = new MessageBody(node.ToJsonString(JsonSerialisationOptions.Options));
            return message;
        }
        catch (JsonException)
        {
            return message;
        }
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
            return _subject!;
        }

        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            return message.Header.Subject!;
        }

        if (Context != null
            && Context.Bag.TryGetValue(JustSayingAttributesName.Subject, out var obj)
            && obj is string subject && !string.IsNullOrEmpty(subject))
        {
            return subject;
        }

        if (!string.IsNullOrEmpty(publication.Subject))
        {
            return publication.Subject!;
        }
        
        if (publication.RequestType != null)
        {
            return publication.RequestType.Name;
        }

        throw new InvalidOperationException("No Subject was define via Attribute/Message/SnsPublication");
    }

    private static void SetId(JsonNode node, Message message)
    {
        var jsonNode = node[nameof(IJustSayingRequest.Id)];
        if (jsonNode != null
            && jsonNode.GetValueKind() == JsonValueKind.String
            && Guid.TryParse(jsonNode.GetValue<string>(), out _))
        {
            return;
        }

        var id = message.Id;
        if (Id.IsNullOrEmpty(id) || !Guid.TryParse(id, out _))
        {
            id = Id.Random;
        }
        
        node[nameof(IJustSayingRequest.Id)] = id.Value;
    }
    
    private static void SetTimeStamp(JsonNode node, Message message)
    {
        var jsonNode = node[nameof(IJustSayingRequest.TimeStamp)];
        if (jsonNode != null
            && jsonNode.GetValueKind() == JsonValueKind.String
            && DateTimeOffset.TryParse(jsonNode.GetValue<string>(), out _))
        {
            return;
        }

        var timestamp = message.Header.TimeStamp;
        if (timestamp == DateTimeOffset.MinValue)
        {
            timestamp = DateTimeOffset.UtcNow;
        }
        
        node[nameof(IJustSayingRequest.TimeStamp)] = timestamp;
    }
    
    private static void SetConversation(JsonNode node, Message message)
    {
        var jsonNode = node[nameof(IJustSayingRequest.Conversation)];
        if (jsonNode != null
            && jsonNode.GetValueKind() == JsonValueKind.String
            && Guid.TryParse(jsonNode.GetValue<string>(), out _))
        {
            return;
        }

        var correlationId = message.Header.CorrelationId;
        if (Id.IsNullOrEmpty(correlationId))
        {
            correlationId = Id.Random;
        }
        
        node[nameof(IJustSayingRequest.Conversation)] = correlationId.Value;
    }
    

    private void SetTenant(JsonNode node)
    {
        var jsonNode = node[nameof(IJustSayingRequest.Tenant)];
        if (jsonNode != null && jsonNode.GetValueKind() == JsonValueKind.String)
        {
            return;
        }

        node[nameof(IJustSayingRequest.Tenant)] =  GetTenant();
        return;
            
        string? GetTenant()
        {
            if (!string.IsNullOrEmpty(_tenant))
            {
                return _tenant;
            }

            if (Context != null 
                && Context.Bag.TryGetValue(JustSayingAttributesName.Tenant, out var obj) 
                && obj is string tenant && !string.IsNullOrEmpty(tenant))
            {
                return tenant;
            }

            return null;
        }
    }

    private void SetRaisingComponent(JsonNode node, Message message, Publication publication)
    {
        var jsonNode = node[nameof(IJustSayingRequest.RaisingComponent)];
        if (jsonNode != null && jsonNode.GetValueKind() == JsonValueKind.String)
        {
            return;
        }

        node[nameof(IJustSayingRequest.RaisingComponent)] = GetRaisingComponent();
        return;

        string GetRaisingComponent()
        {
            if (!string.IsNullOrEmpty(_raisingComponent))
            {
                return _raisingComponent!;
            }

            if (Context != null
                && Context.Bag.TryGetValue(JustSayingAttributesName.RaisingComponent, out var obj)
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
    }

    private void SetVersion(JsonNode node)
    {
        var jsonNode = node[nameof(IJustSayingRequest.Version)];
        if (jsonNode != null && jsonNode.GetValueKind() == JsonValueKind.String)
        {
            return;
        }
        
        node[nameof(IJustSayingRequest.Version)] = GetVersion();
        return;
        
        string GetVersion()
        {
            if (!string.IsNullOrEmpty(_version))
            {
                return _version!;
            }

            if (Context != null
                && Context.Bag.TryGetValue(JustSayingAttributesName.Version, out var obj)
                && obj is string version && !string.IsNullOrEmpty(version))
            {
                return version;
            }

            return "1.0.0";
        }
    }
}
