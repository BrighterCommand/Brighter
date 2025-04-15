using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private string? _dataContentType;
    private Uri? _dataSchema;
    private string? _subject;
    private CloudEventFormat _format;

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

        if (_format == CloudEventFormat.Json)
        {
            var cloudEvent = new CloudEventMessage
            {
                Id = message.Id,
                SpecVersion = message.Header.SpecVersion,
                Source = message.Header.Source,
                Type = message.Header.Type,
                DataContentType = message.Header.ContentType,
                DateSchema = message.Header.DataSchema,
                Subject = message.Header.Subject,
                Time = message.Header.TimeStamp,
                Data = JsonSerializer.Deserialize<JsonElement>(message.Body.Value)
            };

            message.Body = new MessageBody(JsonSerializer.Serialize(cloudEvent, JsonSerialisationOptions.Options));
            message.Header.ContentType = "application/cloudevents+json";
        }

        return message;
    }

    public Message Unwrap(Message message)
    {
        if (_format == CloudEventFormat.Json)
        {
            var cloudEvents =
                JsonSerializer.Deserialize<CloudEventMessage>(message.Body.Value, JsonSerialisationOptions.Options);
            if (cloudEvents != null)
            {
                var bag = cloudEvents.AdditionalProperties ?? [];
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
                    DataSchema = cloudEvents.DateSchema,
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

                var body = new MessageBody(cloudEvents.Data.ToString());
                return new Message(header, body);
            }
        }

        return message;
    }

    public class CloudEventMessage
    {
        [JsonPropertyName("id")] 
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("specversion")] 
        public string SpecVersion { get; set; } = "1.0";

        [JsonPropertyName("source")]
        public Uri Source { get; set; } = new(MessageHeader.DefaultSource);

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("datacontenttype")]
        public string? DataContentType { get; set; }

        [JsonPropertyName("dataschema")]
        public Uri? DateSchema { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("time")]
        public DateTimeOffset? Time { get; set; }
        
        [JsonExtensionData]
        public Dictionary<string, object>? AdditionalProperties { get; set; }

        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }
}
