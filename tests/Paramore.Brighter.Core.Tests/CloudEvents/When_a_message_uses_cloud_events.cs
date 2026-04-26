using System;
using System.Net.Mime;
using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.CloudEvents;
public class CloudEventsTransformerTests
{
    private readonly CloudEventsTransformer _transformer = new();
    private readonly Uri _source = new("http://goparamore.io/CloudEventsTransformerTests");
    private readonly CloudEventsType _type = new(typeof(MyCommand).FullName ?? "MyCommand");
    private readonly Uri _dataSchema = new Uri("http://goparamore.io/CloudEventsTransformerTests/schema");
    private readonly string _subject = "CloudEventsTransformerTests";
    private Message _message;
    public CloudEventsTransformerTests()
    {
        _message = new(new MessageHeader(Id.Random(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND, contentType: new ContentType(MediaTypeNames.Text.Plain), source: _source, subject: _subject, type: _type, dataSchema: _dataSchema), new MessageBody("test content", contentType: new ContentType(MediaTypeNames.Text.Plain)));
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_via_attribute()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.ToString(), _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
        var cloudEvents = _transformer.Wrap(_message, new Publication { DataSchema = _dataSchema, Source = _source, Type = _type, Subject = _subject });
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(_source);
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(_type);
        await Assert.That(cloudEvents.Header.ContentType!).IsEqualTo(_message.Header.ContentType);
        await Assert.That(cloudEvents.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(_subject);
        await Assert.That(cloudEvents.Body.Bytes).IsEquivalentTo(body.Bytes);
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication { DataSchema = _dataSchema, Source = _source, Type = _type, Subject = _subject });
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(_source);
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(_type);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(cloudEvents.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(_subject);
        await Assert.That(cloudEvents.Body.Bytes).IsNotEqualTo(body.Bytes);
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(json).IsNotNull();
        await Assert.That(json.Source).IsEqualTo(_source);
        await Assert.That(json.Type).IsEqualTo(_type);
        await Assert.That(json.DataContentType).IsEqualTo(new ContentType(MediaTypeNames.Text.Plain).MediaType);
        await Assert.That(json.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(json.Subject).IsEqualTo(_subject);
        await Assert.That(json.Data?.GetString()).IsEqualTo(body.Value);
    }

    [Test]
    public async Task When_a_empty_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), new MessageBody([]));
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication { DataSchema = _dataSchema, Source = _source, Type = _type, Subject = _subject });
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(_source);
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(_type);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(new ContentType("application/cloudevents+json"));
        await Assert.That(cloudEvents.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(_subject);
        await Assert.That(cloudEvents.Body.Bytes).IsNotEqualTo(body.Bytes);
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(json).IsNotNull();
        await Assert.That(json.Source).IsEqualTo(_source);
        await Assert.That(json.Type).IsEqualTo(_type);
        await Assert.That(json.DataContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }.ToString());
        await Assert.That(json.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(json.Subject).IsEqualTo(_subject);
        await Assert.That(json.Data).IsNull();
    }

    [Test]
    public async Task When_a_non_json_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), new MessageBody("teste"));
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication { DataSchema = _dataSchema, Source = _source, Type = _type, Subject = _subject });
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(_source);
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(_type);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(cloudEvents.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(_subject);
        await Assert.That(cloudEvents.Body.Bytes).IsEquivalentTo(body.Bytes);
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_via_a_publication()
    {
        //act
        var cloudEvents = _transformer.Wrap(_message, new Publication { Source = _source, Type = _type, DataSchema = _dataSchema, Subject = _subject });
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(_source);
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(_type);
        await Assert.That(cloudEvents.Header.DataSchema!.AbsoluteUri).IsEqualTo(_dataSchema.AbsoluteUri);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(_subject);
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_and_a_publication_and_format_as_binary()
    {
        //arrange
        var publication = new Publication
        {
            Source = _source,
            Type = _type,
            DataSchema = _dataSchema,
            Subject = _subject
        };
        //These attributes override the publication values above
        const string source = "http://goparamore.io/OverrideSource";
        const string type = "goparamore.io/OverrideType";
        //Should use a string, as we want to pass these as constants in the attribute
        string dataSchema = "http://goparamore.io/CloudEventsTransformerOverride/schema";
        const string subject = "CloudEventsTransformerAlternative";
        //act
        _transformer.InitializeWrapFromAttributeParams(source, type, null, dataSchema, subject, CloudEventFormat.Binary);
        var cloudEvents = _transformer.Wrap(_message, publication);
        //assert
        await Assert.That(cloudEvents.Header.Source.ToString()).IsEqualTo(source);
        await Assert.That(cloudEvents.Header.Type.Value).IsEqualTo(type);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(cloudEvents.Header.DataSchema!.AbsoluteUri).IsEqualTo(dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(subject);
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_and_a_publication_and_format_as_json()
    {
        //arrange
        var publication = new Publication
        {
            Source = _source,
            Type = _type,
            DataSchema = _dataSchema,
            Subject = _subject
        };
        //These attributes override the publication values above
        const string source = "http://goparamore.io/OverrideSource";
        const string type = "goparamore.io/OverrideType";
        const string dataContentType = MediaTypeNames.Text.Plain;
        var cloudEventDataContentType = new ContentType("application/cloudevents+json");
        const string dataSchema = "http://goparamore.io/CloudEventsTransformerOverride/schema";
        const string subject = "CloudEventsTransformerAlternative";
        var body = _message.Body;
        //act
        _transformer.InitializeWrapFromAttributeParams(source, type, null, dataSchema, subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, publication);
        //assert
        await Assert.That(cloudEvents.Header.Source.ToString()).IsEqualTo(source);
        await Assert.That(cloudEvents.Header.Type.Value).IsEqualTo(type);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(cloudEventDataContentType);
        await Assert.That(cloudEvents.Header.DataSchema!.ToString()).IsEqualTo(dataSchema);
        await Assert.That(cloudEvents.Header.Subject).IsEqualTo(subject);
        await Assert.That(cloudEvents.Body.Bytes).IsNotEqualTo(body.Bytes);
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        await Assert.That(json).IsNotNull();
        await Assert.That(json.Source.ToString()).IsEqualTo(source);
        await Assert.That(json.Type).IsEqualTo(type);
        await Assert.That(json.DataContentType).IsEqualTo(dataContentType);
        await Assert.That(json.DataSchema!.ToString()).IsEqualTo(dataSchema);
        await Assert.That(json.Subject).IsEqualTo(subject);
        await Assert.That(json.Data?.GetString()).IsEqualTo(body.Value);
    }

    [Test]
    public async Task When_a_message_uses_cloud_events_but_no_publication_or_arguments()
    {
        //arrange
        var publication = new Publication
        {
        //no cloud events properties set
        };
        //These attributes override the publication values above
        //act
        var cloudEvents = _transformer.Wrap(_message, publication);
        //assert
        await Assert.That(cloudEvents.Header.Source).IsEqualTo(new Uri("http://goparamore.io"));
        await Assert.That(cloudEvents.Header.Type).IsEqualTo(CloudEventsType.Empty);
        await Assert.That(cloudEvents.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Text.Plain));
        await Assert.That(cloudEvents.Header.DataSchema).IsNull();
        await Assert.That(cloudEvents.Header.Subject).IsNull();
    }

    [Test]
    public async Task When_unwrap_a_message_with_binary_format()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
        // act
        var message = _transformer.Unwrap(_message);
        await Assert.That(message.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(message.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(message.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(message.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(message.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(message.Body.Bytes).IsEquivalentTo(_message.Body.Bytes);
    }

    [Test]
    public async Task When_unwrap_a_message_with_binary_format_and_wrap_before()
    {
        //arrange
        var publication = new Publication
        {
        //no cloud events properties set
        };
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
        var cloudEvents = _transformer.Wrap(_message, publication);
        // act
        var message = _transformer.Unwrap(cloudEvents);
        await Assert.That(message.Header.Source).IsEqualTo(_source);
        await Assert.That(message.Header.Type).IsEqualTo(CloudEventsType.Empty);
        await Assert.That(message.Header.DataSchema).IsEqualTo(_dataSchema);
        await Assert.That(message.Header.Subject).IsEqualTo(_subject);
        await Assert.That(message.Body.Bytes).IsEquivalentTo(_message.Body.Bytes);
    }

    [Test]
    public async Task When_unwrap_a_message_with_json_format_and_provided_message_is_not_json()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), new MessageBody("teste"));
        // act
        var message = _transformer.Unwrap(_message);
        await Assert.That(message.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(message.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(message.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(message.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(message.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(message.Body.Bytes).IsEquivalentTo(_message.Body.Bytes);
    }

    [Test]
    public async Task When_unwrap_a_message_with_json_format_and_provided_message_is_not_cloud_event_json()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        // act
        var message = _transformer.Unwrap(_message);
        await Assert.That(message.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(message.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(message.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(message.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(message.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(message.Body.Bytes).IsEquivalentTo(_message.Body.Bytes);
    }

    [Test]
    public async Task When_unwrap_a_message_with_json_format_and_provided_message_has_no_body()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), new MessageBody([]));
        var cloudEventsMessage = _transformer.Wrap(_message, new Publication { DataSchema = _dataSchema, Source = _source, Type = _type, Subject = _subject });
        // act
        var unwrap = _transformer.Unwrap(cloudEventsMessage);
        await Assert.That(unwrap.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(unwrap.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(unwrap.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
        await Assert.That(unwrap.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(unwrap.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(unwrap.Body.Bytes).IsEquivalentTo(Array.Empty<byte>());
    }

    [Test]
    public async Task When_unwrap_a_message_with_json_format()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var body = _message.Body;
        var cloudEventsMessage = _transformer.Wrap(_message, new Publication { CloudEventsAdditionalProperties = new Dictionary<string, object> { ["test"] = "test-header" } });
        // act
        var unwrap = _transformer.Unwrap(cloudEventsMessage);
        await Assert.That(unwrap.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(unwrap.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(unwrap.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Text.Plain));
        await Assert.That(unwrap.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(unwrap.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(unwrap.Body.Bytes).IsEquivalentTo(body.Bytes);
        await Assert.That(unwrap.Header.Bag).HasSingleItem();
        await Assert.That(unwrap.Header.Bag["test"]).IsEqualTo("test-header");
    }
}