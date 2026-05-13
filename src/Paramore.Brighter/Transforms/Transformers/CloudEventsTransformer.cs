using System;
using System.Net.Mime;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Transforms.Transformers;

/// <summary>
/// Provides support for the <see href="https://github.com/cloudevents/spec?tab=readme-ov-file">Cloud Events specification</see>
/// by ensuring that our message has the required metadata to support Cloud Events.
///
/// All settable attributes follow the same precedence (highest priority first):
///   1. <see cref="CloudEventsAttribute"/> parameters (hardcoded on the attribute)
///   2. Message header values already set by the message mapper
///   3. <see cref="Publication"/> properties (fallback defaults)
///
/// The following Cloud Events attributes are supported:
/// REQUIRED
///     id => the message id from <see cref="MessageHeader"/>; you don't set this here, as we use the id from the <see cref="Request"/>
///     source => attribute > mapper > publication
///     specversion => attribute > mapper; defaults to 1.0
///     type => attribute > mapper > publication
/// OPTIONAL
///     datacontenttype => attribute > mapper (no publication fallback)
///     dataschema => attribute > mapper > publication
///     subject => attribute > mapper > publication
///     time => sets the timestamp for <see cref="MessageHeader"/>
/// </summary>
public partial class CloudEventsTransformer : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CloudEventsTransformer>();
    private static readonly Uri s_defaultSource = new(MessageHeader.DefaultSource);

    private Uri? _source;
    private string? _type;
    private string? _specVersion;
    private ContentType? _dataContentType;
    private Uri? _dataSchema;
    private string? _subject;
    private CloudEventFormat _format;

    /// <summary>
    /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
    /// </summary>
    /// <value>The context.</value>
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        //no op as we have no unmanaged resources
    }

    /// <summary>
    /// Initializes the transformer with parameters from the <see cref="CloudEventsAttribute"/>.
    /// </summary>
    /// <param name="initializerList">
    /// 0 - <see cref="CloudEventsAttribute.Source"/> as a string, which will be converted to a <see cref="Uri"/>.
    /// 1 - <see cref="CloudEventsAttribute.Type"/> as a string.
    /// 2 - <see cref="CloudEventsAttribute.SpecVersion"/> as a string, defaults to "1.0".
    /// 3 - <see cref="CloudEventsAttribute.DataContentType"/> as a string, which will be converted to a <see cref="ContentType"/>.
    /// 4 - <see cref="CloudEventsAttribute.DataSchema"/> as a string, which will be converted to a <see cref="Uri"/>.
    /// 5 - <see cref="CloudEventsAttribute.Subject"/> as a string.
    /// 6 - <see cref="CloudEventsAttribute.Format"/> as a <see cref="CloudEventFormat"/>.
    /// </param>
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

        if (initializerList[6] is CloudEventFormat format)
        {
            _format = format;
        }
    }

    /// <inheritdoc cref="IAmAMessageTransform.InitializeUnwrapFromAttributeParams" />
    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList[0] is CloudEventFormat format)
        {
            _format = format;
        } 
    }

    /// <inheritdoc />
    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
    {
        return Task.FromResult(Wrap(message, publication));
    }

    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.FromResult(Unwrap(message));
    }

    /// <inheritdoc />
    public Message Wrap(Message message, Publication publication)
    {
        var msg =  WritePublicationHeaders(message,  publication);
        return _format == CloudEventFormat.Binary ? msg : WriteJsonMessage(msg, publication);
    }


    /// <inheritdoc />
    public Message Unwrap(Message message)
    {
        if (_format == CloudEventFormat.Binary)
        {
            return message;
        }

        return ReadCloudEventJsonMessage(message);
    }

    private static Message ReadCloudEventJsonMessage(Message message)
    {
        try
        {
            #if NETSTANDARD2_0
            var cloudEvents = JsonSerializer.Deserialize<JsonEvent>(message.Body.Memory.ToArray(), JsonSerialisationOptions.Options);
#else
            var cloudEvents = JsonSerializer.Deserialize<JsonEvent>(message.Body.Memory.Span, JsonSerialisationOptions.Options);
#endif
            if (cloudEvents == null)
            {
                return message;
            }

            var bag = new Dictionary<string, object>(cloudEvents.AdditionalProperties ?? new Dictionary<string, object>());
            foreach (KeyValuePair<string, object> pair in message.Header.Bag)
            {
                bag[pair.Key] = pair.Value;
            }

            var header = new MessageHeader
            {
                MessageId = cloudEvents.Id,
                SpecVersion = cloudEvents.SpecVersion,
                Source = cloudEvents.Source,
                Type = new CloudEventsType(cloudEvents.Type),
                ContentType = new ContentType(cloudEvents.DataContentType!),
                DataSchema = cloudEvents.DataSchema,
                Subject = cloudEvents.Subject,
                TimeStamp = cloudEvents.Time ?? DateTimeOffset.UtcNow,
                CorrelationId = message.Header.CorrelationId,
                DataRef = message.Header.DataRef,
                Delayed = message.Header.Delayed,
                HandledCount = message.Header.HandledCount,
                MessageType = message.Header.MessageType,
                PartitionKey = message.Header.PartitionKey,
                ReplyTo = message.Header.ReplyTo,
                Topic = message.Header.Topic,
                TraceParent = message.Header.TraceParent,
                TraceState = message.Header.TraceState,
                Bag = bag
            };
           
            MessageBody body;
            if (!string.IsNullOrEmpty(cloudEvents.DataBase64))
            {
                // Binary data: decode from base64
                var bytes = Convert.FromBase64String(cloudEvents.DataBase64!);
                body = new MessageBody(bytes);
            }
            else if (cloudEvents.Data.HasValue)
            {
                // JSON or string data
                body = cloudEvents.Data.Value.ValueKind == JsonValueKind.String 
                    ? new MessageBody(cloudEvents.Data.Value.GetString() ?? string.Empty) 
                    : new MessageBody(cloudEvents.Data.Value.GetRawText());
            }
            else
            {
                body = new MessageBody(string.Empty);
            }
            
            return new Message(header, body);
        }
        catch(JsonException ex)
        {
            Log.ErrorDuringDeserializerOnUnwrap(s_logger, ex);
            return message;
        }
    }

    private Message WritePublicationHeaders(Message message, Publication publication)
    {
        // Precedence for all attributes: attribute params > message header (set by mapper) > publication (fallback)
        // Source and Type use sentinel checks (s_defaultSource / CloudEventsType.Empty) because their defaults
        // are non-null, so we need a way to distinguish "mapper didn't set it" from "mapper set it explicitly."
        // DataSchema, Subject, and DataContentType default to null, so plain null-coalescing suffices.
        message.Header.Source = _source
            ?? (!Equals(message.Header.Source, s_defaultSource) ? message.Header.Source : publication.Source);
        message.Header.Type = _type is not null
            ? new CloudEventsType(_type)
            : (message.Header.Type != CloudEventsType.Empty ? message.Header.Type : publication.Type);
        message.Header.ContentType = _dataContentType ?? message.Header.ContentType;
        message.Header.DataSchema = _dataSchema ?? message.Header.DataSchema ?? publication.DataSchema;
        message.Header.Subject = _subject ?? message.Header.Subject ?? publication.Subject;
        message.Header.SpecVersion = _specVersion ?? message.Header.SpecVersion;
        return message;
    }
    
    private Message WriteJsonMessage(Message message, Publication publication)
    {
        try
        {
            JsonElement? data = null;
            string? dataBase64 = null;
            var contentType = message.Header.ContentType.ToString()?? string.Empty;
            if (message.Body.Value.Length > 0)
            {
                if (contentType.Contains("application/json") || contentType.Contains("text/json"))
                {
                    data = JsonSerializer.Deserialize<JsonElement>(message.Body.Value, JsonSerialisationOptions.Options);
                }
                else if (contentType.Contains("application/octet-stream"))
                {
                    // Base64 encode binary data and use data_base64
#if NETSTANDARD2_0
                    dataBase64 = Convert.ToBase64String(message.Body.Memory.ToArray());
#else
                    dataBase64 = Convert.ToBase64String(message.Body.Memory.Span);
#endif
                }
                else
                {
                    // Properly encode the value as a JSON string
                    var encoded = JsonEncodedText.Encode(message.Body.Value);
                    data = JsonDocument.Parse($"\"{encoded.ToString()}\"").RootElement;
                }
            }
            
            var defaultCloudEventsAdditionalProperties = publication.CloudEventsAdditionalProperties ?? new Dictionary<string, object>();

            var cloudEvent = new JsonEvent
            {
                Id = message.Id,
                SpecVersion = message.Header.SpecVersion,
                Source = message.Header.Source,
                Type = message.Header.Type,
                DataContentType = contentType,
                DataSchema = message.Header.DataSchema,
                Subject = message.Header.Subject,
                Time = message.Header.TimeStamp,
                AdditionalProperties = defaultCloudEventsAdditionalProperties.Merge(Context.GetCloudEventAdditionalProperties()),
                Data = data,
                DataBase64 = dataBase64 // Add this property to CloudEventMessage
            };

            message.Body = new MessageBody(JsonSerializer.SerializeToUtf8Bytes(cloudEvent, JsonSerialisationOptions.Options));
            message.Header.ContentType = new ContentType("application/cloudevents+json");

            return message;
        }
        catch (JsonException e)
        {
            Log.ErrorDuringDeserializerAJsonOnWrap(s_logger, e);
            return message;
        }
    }

    /// <summary>
    /// Represents a CloudEvent message envelope, adhering to the CloudEvents specification.
    /// This class uses <see cref="JsonElement"/> for the 'data' payload, providing flexibility
    /// in handling various JSON structures without strong typing at this level.
    /// </summary>
    public class JsonEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier for the event.
        /// Complies with the 'id' attribute of the CloudEvents specification.
        /// Defaults to an empty string.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("id")]
        public Id Id { get; set; } = Id.Empty;

        /// <summary>
        /// Gets or sets the specification version of the CloudEvents specification which the event uses.
        /// Complies with the 'specversion' attribute.
        /// Defaults to "1.0".
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("specversion")]
        public string SpecVersion { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the source of the event.
        /// Complies with the 'source' attribute, identifying the context in which an event happened.
        /// Defaults to the <see cref="MessageHeader.DefaultSource"/>.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("source")]
        public Uri Source { get; set; } = new(MessageHeader.DefaultSource);

        /// <summary>
        /// Gets or sets the type of the event.
        /// Complies with the 'type' attribute, describing the kind of event related to the originating occurrence.
        /// Defaults to an empty string.
        /// </summary>
        [JsonRequired]
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
        /// Gets or sets the event data payload as a <see cref="JsonElement"/>.
        /// Complies with the 'data' attribute.
        /// This allows for deferred deserialization or direct manipulation of the JSON data.
        /// </summary>
        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }

        /// <summary>
        /// Used for binary data in CloudEvents.
        /// </summary>
        [JsonPropertyName("data_base64")]
        public string? DataBase64 { get; set; }
    }

    internal static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Error during deserialization a JSON on wrap")]
        public static partial void ErrorDuringDeserializerAJsonOnWrap(ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error during deserialization a Cloud Event JSON on unwrap")]
        public static partial void ErrorDuringDeserializerOnUnwrap(ILogger logger, Exception ex);
    }
}
