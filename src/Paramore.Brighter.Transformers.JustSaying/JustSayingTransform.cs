using System;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying.Extensions;

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
        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            return message.Header.Subject!;
        }

        var subject = Context.GetFromBag<string>(JustSayingAttributesName.Subject);
        if (!string.IsNullOrEmpty(subject))
        {
            return subject!;
        }
        
        if (!string.IsNullOrEmpty(_subject))
        {
            return _subject!;
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
        var guid = node.GetGuid(nameof(IJustSayingRequest.Id));
        if (guid != Guid.Empty)
        {
            return;
        }

        var id = message.Id;
        if (Id.IsNullOrEmpty(id) || !Guid.TryParse(id, out _))
        {
            id = Id.Random();
        }
        
        node[nameof(IJustSayingRequest.Id)] = id.Value;
    }
    
    private void SetTimeStamp(JsonNode node, Message message)
    {
        node[nameof(IJustSayingRequest.TimeStamp)] = GetTimeStamp(message, node.GetDateTimeOffset(nameof(IJustSayingRequest.TimeStamp)));
    }
    
    private static DateTime GetTimeStamp(Message message, DateTimeOffset timestamp)
    {
        if (timestamp != DateTimeOffset.MinValue)
        {
            return timestamp.DateTime;
        }

        if (message.Header.TimeStamp != DateTimeOffset.MinValue)
        {
            return message.Header.TimeStamp.DateTime;
        }

        return DateTime.UtcNow;
    }
    
    private void SetConversation(JsonNode node, Message message)
    {
        node[nameof(IJustSayingRequest.Conversation)] = GetConversation(message, node.GetId(nameof(IJustSayingRequest.Conversation))).Value;
    }
    
    private Id GetConversation(Message message, Id? conversation)
    {
        if (!Id.IsNullOrEmpty(conversation))
        {
            return conversation;
        }

        if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
        {
            return message.Header.CorrelationId;
        }
        
        return Context.GetIdFromBag(JustSayingAttributesName.Conversation) ?? Id.Random();
    }

    private void SetTenant(JsonNode node) => node[nameof(IJustSayingRequest.Tenant)] =  GetTenant(node.GetString(nameof(IJustSayingRequest.Tenant)));

    private string? GetTenant(string? tenant)
    {
        if (!string.IsNullOrEmpty(tenant))
        {
            return tenant;
        }
        
        if (!string.IsNullOrEmpty(_tenant))
        {
            return _tenant;
        }

        return Context.GetFromBag<string>(JustSayingAttributesName.Tenant);
    }

    private void SetRaisingComponent(JsonNode node, Message message, Publication publication) =>
        node[nameof(IJustSayingRequest.RaisingComponent)] = GetRaisingComponent(message, publication, 
            node.GetString(nameof(IJustSayingRequest.RaisingComponent)));

    private static readonly Uri s_defaultSource = new Uri(MessageHeader.DefaultSource);
    private string GetRaisingComponent(Message message, Publication publication, string? raisingComponent)
    {
        if (!string.IsNullOrEmpty(raisingComponent))
        {
            return raisingComponent!;
        }
        
        if (message.Header.Source != s_defaultSource)
        {
            return message.Header.Source.ToString();
        }
        
        raisingComponent = Context.GetFromBag<string>(JustSayingAttributesName.RaisingComponent);
        if (!string.IsNullOrEmpty(raisingComponent))
        {
            return raisingComponent!;
        }
        
        if (!string.IsNullOrEmpty(_raisingComponent))
        {
            return _raisingComponent!;
        }

        return publication.Source.ToString();
    }

    private void SetVersion(JsonNode node) => node[nameof(IJustSayingRequest.Version)] = GetVersion(node.GetString(nameof(IJustSayingRequest.Version)));

    private string? GetVersion(string? version)
    {
        if (!string.IsNullOrEmpty(version))
        {
            return version!;
        }

        if (!string.IsNullOrEmpty(_version))
        {
            return _version;
        }

        return Context.GetFromBag<string>(JustSayingAttributesName.Version);
    }
}
