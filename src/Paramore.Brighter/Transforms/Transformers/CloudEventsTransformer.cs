using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
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
public partial class CloudEventsTransformer : IAmAMessageTransform
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<CloudEventsTransformer>();

    private Uri? _source;
    private string? _type;
    private string? _specVersion;
    private string? _dataContentType;
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

    /// <inheritdoc />
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
            _dataContentType = dataContentType;
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

    /// <inheritdoc />
    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }

    /// <inheritdoc />
    public Message Wrap(Message message, Publication publication)
    {
        message.Header.Source = _source ?? publication.Source;
        message.Header.Type = _type ?? publication.Type;
        message.Header.DataSchema = _dataSchema ?? publication.DataSchema;
        message.Header.Subject = _subject ?? publication.Subject;
        message.Header.ContentType = _dataContentType ?? publication.ContentType;
        message.Header.SpecVersion = _specVersion ?? message.Header.SpecVersion;

        foreach (var additional in publication.CloudEventsAdditionalProperties ?? new Dictionary<string, object>())
        {
            if (!message.Header.Bag.ContainsKey(additional.Key))
            {
                message.Header.Bag[additional.Key] = additional.Value;
            }
        }

        if (_format == CloudEventFormat.Binary)
        {
            return message;
        }

        try
        {
            JsonElement? data = null;
            if (message.Body.Value.Length > 0)
            {
                data = JsonSerializer.Deserialize<JsonElement>(message.Body.Value);
            }
            
            var cloudEvent = new CloudEventMessage
            {
                Id = message.Id,
                SpecVersion = message.Header.SpecVersion,
                Source = message.Header.Source,
                Type = message.Header.Type,
                DataContentType = message.Header.ContentType,
                DataSchema = message.Header.DataSchema,
                Subject = message.Header.Subject,
                Time = message.Header.TimeStamp,
                AdditionalProperties = message.Header.Bag,
                Data = data 
            };

            message.Body = new MessageBody(JsonSerializer.Serialize(cloudEvent, JsonSerialisationOptions.Options));
            message.Header.ContentType = "application/cloudevents+json";

            return message;
        }
        catch (JsonException e)
        {
            Log.ErrorDuringDeserializerAJsonOnWrap(s_logger, e);
            return message;
        }
    }

    /// <inheritdoc />
    public Message Unwrap(Message message)
    {
        if (_format == CloudEventFormat.Binary)
        {
            return message;
        }

        try
        {
            var cloudEvents = JsonSerializer.Deserialize<CloudEventMessage>(message.Body.Value, JsonSerialisationOptions.Options);
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
                Type = cloudEvents.Type,
                ContentType = cloudEvents.DataContentType,
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

            var body = new MessageBody(cloudEvents.Data?.ToString());
            return new Message(header, body);
        }
        catch(JsonException ex)
        {
            Log.ErrorDuringDeserializerOnUnwrap(s_logger, ex);
            return message;
        }
    }

    /// <summary>
    /// Represents a CloudEvent message envelope, adhering to the CloudEvents specification.
    /// This class uses <see cref="JsonElement"/> for the 'data' payload, providing flexibility
    /// in handling various JSON structures without strong typing at this level.
    /// </summary>
    public class CloudEventMessage
    {
        /// <summary>
        /// Gets or sets the unique identifier for the event.
        /// Complies with the 'id' attribute of the CloudEvents specification.
        /// Defaults to an empty string.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

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
    }

    internal static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Error during deserialization a JSON on wrap")]
        public static partial void ErrorDuringDeserializerAJsonOnWrap(ILogger logger, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error during deserialization a Cloud Event JSON on unwrap")]
        public static partial void ErrorDuringDeserializerOnUnwrap(ILogger logger, Exception ex);
    }
}
