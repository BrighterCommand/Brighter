using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessageMappers;

/// <summary>
/// A generic message mapper that serializes and deserializes request objects of type <typeparamref name="TRequest"/>
/// to and from Cloud Events JSON format for use with Brighter's messaging infrastructure. This mapper implements both synchronous
/// and asynchronous interfaces for mapping messages. It supports mapping both <see cref="ICommand"/> and <see cref="IEvent"/> types. 
/// </summary>
/// <typeparam name="TRequest">The message type.</typeparam>
public class CloudEventJsonMessageMapper<TRequest> : IAmAMessageMapper<TRequest>, IAmAMessageMapperAsync<TRequest> where TRequest : class, IRequest
{
    /// <inheritdoc cref="IAmAMessageMapper{TRequest}.Context"/>
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public Task<Message> MapToMessageAsync(TRequest request, Publication publication,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MapToMessage(request, publication));

    /// <inheritdoc />
    public Task<TRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        => Task.FromResult(MapToRequest(message));

    /// <inheritdoc />
    public Message MapToMessage(TRequest request, Publication publication)
    {
        var messageType = request switch
        {
            ICommand => MessageType.MT_COMMAND,
            IEvent => MessageType.MT_EVENT,
            _ => throw new ArgumentException(@"This message mapper can only map Commands and Events", nameof(request))
        };

        if (publication.Topic is null)
        {
            throw new ArgumentException($"No Topic Defined for {publication}");
        }
        
        var defaultHeaders = publication.DefaultHeaders ?? new Dictionary<string, object>();
        var headerContentType = new ContentType("application/cloudevents+json");
        var header = new MessageHeader(
            messageId: request.Id,
            topic: publication.Topic,
            messageType: messageType,
            contentType: headerContentType,
            partitionKey: Context.GetPartitionKey(),
            source: publication.Source,
            type: publication.Type,
            dataSchema: publication.DataSchema,
            subject: publication.Subject
        )
        {
            Bag = defaultHeaders.Merge(Context.GetHeaders()),
        };
        
#if NETSTANDARD2_0
        var bodyContentType = new ContentType("application/json");
 #else           
        var bodyContentType = new ContentType(MediaTypeNames.Application.Json);
#endif
        
        var defaultCloudEventsAdditionalProperties = publication.CloudEventsAdditionalProperties ?? new Dictionary<string, object>();
        var body = new MessageBody(JsonSerializer.Serialize(new CloudEventMessage
        {
            Id = request.Id,
            Source = publication.Source,
            Type = publication.Type,
            DataContentType = bodyContentType.ToString(),
            Subject = publication.Subject,
            DataSchema = publication.DataSchema,
            AdditionalProperties = defaultCloudEventsAdditionalProperties.Merge(Context.GetCloudEventAdditionalProperties()),
            Time = DateTimeOffset.UtcNow,
            Data = request
        }, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    /// <inheritdoc />
    public TRequest MapToRequest(Message message)
    {
        var request = JsonSerializer.Deserialize<CloudEventMessage>(message.Body.Value, JsonSerialisationOptions.Options);

        if (request is null)
        {
            throw new ArgumentException($"Unable to deseralise message body for {message.Header.Topic}");
        }

        return request.Data;
    }
    
    /// <summary>
    /// Represents a CloudEvent message envelope, adhering to the CloudEvents specification.
    /// This generic class wraps the event data of type <typeparamref cref="TRequest"/>.
    /// </summary>
    public class CloudEventMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier for the event.
        /// Complies with the 'id' attribute of the CloudEvents specification.
        /// Defaults to an empty string.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specification version of the CloudEvents specification which the event uses.
        /// Complies with the 'specversion' attribute.
        /// Defaults to "1.0".
        /// </summary>
        [JsonPropertyName("specversion")]
        public string SpecVersion { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the source of the event.
        /// Complies with the 'source' attribute, identifying the context in which an event happened.
        /// Defaults to the <see cref="MessageHeader.DefaultSource"/>.
        /// </summary>
        [JsonPropertyName("source")]
        public Uri Source { get; set; } = new(MessageHeader.DefaultSource);

        /// <summary>
        /// Gets or sets the type of the event.
        /// Complies with the 'type' attribute, describing the kind of event related to the originating occurrence.
        /// Defaults to an empty string.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content type of the 'data' payload.
        /// Complies with the optional 'datacontenttype' attribute.
        /// Examples include "application/json", "application/xml", etc.
        /// </summary>
        [JsonPropertyName("datacontenttype")]
        public string? DataContentType { get; set; }

        /// <summary>
        /// Gets or sets the schema URI for the 'data' payload.
        /// Complies with the optional 'dataschema' attribute, providing a link to the schema definition.
        /// </summary>
        [JsonPropertyName("dataschema")]
        public Uri? DataSchema { get; set; }

        /// <summary>
        /// Gets or sets the subject of the event in the context of the event producer.
        /// Complies with the optional 'subject' attribute.
        /// </summary>
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the occurrence took place.
        /// Complies with the optional 'time' attribute.
        /// </summary>
        [JsonPropertyName("time")]
        public DateTimeOffset? Time { get; set; }

        /// <summary>
        /// Gets or sets a dictionary for any additional CloudEvents attributes not explicitly defined in this class.
        /// Uses the <see cref="JsonExtensionDataAttribute"/> for serialization and deserialization of these properties.
        /// </summary>
        [JsonExtensionData]
        public IDictionary<string, object>? AdditionalProperties { get; set; }

        /// <summary>
        /// Gets or sets the event data payload.
        /// Complies with the 'data' attribute.
        /// This is the actual event content of type <typeparamref cref="TRequest"/>.
        /// </summary>
        [JsonPropertyName("data")]
        public TRequest Data { get; set; } = null!;
    }   
}
