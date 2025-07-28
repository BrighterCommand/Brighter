using System;
using System.Net.Mime;
using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsTransformerTests 
{
    private readonly CloudEventsTransformer _transformer = new();
    private readonly Uri _source = new("http://goparamore.io/CloudEventsTransformerTests");
    private readonly string _type = typeof(MyCommand).FullName ?? "MyCommand";
    private readonly ContentType _dataContentType = new(MediaTypeNames.Application.Json);
    private readonly Uri _dataSchema = new Uri("http://goparamore.io/CloudEventsTransformerTests/schema");
    private readonly string _subject = "CloudEventsTransformerTests";
    private Message _message;

    public CloudEventsTransformerTests()
    {
        _message = new(
            new MessageHeader(Id.Random, new RoutingKey("Test Topic"), MessageType.MT_COMMAND, contentType: new ContentType(MediaTypeNames.Text.Plain), 
                source: _source, subject: _subject, type: _type, dataSchema: _dataSchema), new MessageBody("test content")
            );
    }

    [Fact]
    public void When_a_message_uses_cloud_events_via_attribute()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.ToString(), _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            DataSchema = _dataSchema,
            Source = _source,
            Type = _type,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_message.Header.ContentType, cloudEvents.Header.ContentType!);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.Equal(body.Bytes, cloudEvents.Body.Bytes);
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            DataSchema = _dataSchema,
            Source = _source,
            Type = _type,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(new ContentType("application/cloudevents+json"), cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.NotEqual(body.Bytes, cloudEvents.Body.Bytes);

        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(json);
        Assert.Equal(_source, json.Source);
        Assert.Equal(_type, json.Type);
        Assert.Equal(new ContentType(MediaTypeNames.Text.Plain).MediaType, json.DataContentType);
        Assert.Equal(_dataSchema, json.DataSchema);
        Assert.Equal(_subject, json.Subject);
        Assert.Equal(body.Value, json.Data?.GetString());
    }
    
    [Fact]
    public void When_a_empty_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody([]) );
        
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null,  _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            DataSchema = _dataSchema,
            Source = _source,
            Type = _type,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(new ContentType("application/cloudevents+json"), cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.NotEqual(body.Bytes, cloudEvents.Body.Bytes);

        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(json);
        Assert.Equal(_source, json.Source);
        Assert.Equal(_type, json.Type);
        Assert.Equal(new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()}.ToString(), json.DataContentType);
        Assert.Equal(_dataSchema, json.DataSchema);
        Assert.Equal(_subject, json.Subject);
        Assert.Null(json.Data);
    }
    
    [Fact]
    public void When_a_non_json_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody("teste"));
        
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            DataSchema = _dataSchema,
            Source = _source,
            Type = _type,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_message.Header.ContentType, cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.Equal(body.Bytes, cloudEvents.Body.Bytes);
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_a_publication()
    {
        //act
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            Source = _source,
            Type = _type,
            DataSchema = _dataSchema,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_dataSchema.AbsoluteUri, cloudEvents.Header.DataSchema!.AbsoluteUri);
        Assert.Equal(_subject, cloudEvents.Header.Subject);

    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_and_a_publication_and_format_as_binary()
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
        _transformer.InitializeWrapFromAttributeParams(source, type, null,  dataSchema, subject, CloudEventFormat.Binary);

        var cloudEvents = _transformer.Wrap(_message, publication);
        
        //assert
        Assert.Equal(source, cloudEvents.Header.Source.ToString());
        Assert.Equal(type, cloudEvents.Header.Type);
        Assert.Equal(_message.Header.ContentType, cloudEvents.Header.ContentType);
        Assert.Equal(dataSchema, cloudEvents.Header.DataSchema!.AbsoluteUri);
        Assert.Equal(subject, cloudEvents.Header.Subject);

    }
    
    
    [Fact]
    public void When_a_message_uses_cloud_events_and_a_publication_and_format_as_json()
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
        _transformer.InitializeWrapFromAttributeParams(source, type, null,  dataSchema, subject, CloudEventFormat.Json);

        var cloudEvents = _transformer.Wrap(_message, publication);
        
        //assert
        Assert.Equal(source, cloudEvents.Header.Source.ToString());
        Assert.Equal(type, cloudEvents.Header.Type);
        Assert.Equal(cloudEventDataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(dataSchema, cloudEvents.Header.DataSchema!.ToString());
        Assert.Equal(subject, cloudEvents.Header.Subject);
        Assert.NotEqual(body.Bytes, cloudEvents.Body.Bytes);
        
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(cloudEvents.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(json);
        Assert.Equal(source, json.Source.ToString());
        Assert.Equal(type, json.Type);
        Assert.Equal(dataContentType, json.DataContentType);
        Assert.Equal(dataSchema, json.DataSchema!.ToString());
        Assert.Equal(subject, json.Subject);
        Assert.Equal(body.Value, json.Data?.GetString());
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_but_no_publication_or_arguments()
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
        Assert.Equal(new Uri("http://goparamore.io"), cloudEvents.Header.Source);
        Assert.Equal( "goparamore.io.Paramore.Brighter.Message", cloudEvents.Header.Type);
        Assert.Equal(new ContentType(MediaTypeNames.Text.Plain), cloudEvents.Header.ContentType);
        Assert.Null(cloudEvents.Header.DataSchema);
        Assert.Null(cloudEvents.Header.Subject);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_binary_format()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null,  _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
       
        // act
        var message = _transformer.Unwrap(_message);
       
        Assert.Equal(_message.Header.Source, message.Header.Source);
        Assert.Equal(_message.Header.Type, message.Header.Type);
        Assert.Equal(_message.Header.ContentType, message.Header.ContentType);
        Assert.Equal(_message.Header.DataSchema, message.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, message.Header.Subject);
        Assert.Equal(_message.Body.Bytes, message.Body.Bytes);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_binary_format_and_wrap_before()
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
       
       Assert.Equal(_source, message.Header.Source);
       Assert.Equal(_type, message.Header.Type);
       Assert.Equal(_dataSchema, message.Header.DataSchema);
       Assert.Equal(_subject, message.Header.Subject);
       Assert.Equal(_message.Body.Bytes, message.Body.Bytes);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_json_format_and_provided_message_is_not_json()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null,  _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody("teste"));
       
        // act
        var message = _transformer.Unwrap(_message);
       
        Assert.Equal(_message.Header.Source, message.Header.Source);
        Assert.Equal(_message.Header.Type, message.Header.Type);
        Assert.Equal(_message.Header.ContentType, message.Header.ContentType);
        Assert.Equal(_message.Header.DataSchema, message.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, message.Header.Subject);
        Assert.Equal(_message.Body.Bytes, message.Body.Bytes);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_json_format_and_provided_message_is_not_cloud_event_json()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
       
        // act
        var message = _transformer.Unwrap(_message);
       
        Assert.Equal(_message.Header.Source, message.Header.Source);
        Assert.Equal(_message.Header.Type, message.Header.Type);
        Assert.Equal(_message.Header.ContentType, message.Header.ContentType);
        Assert.Equal(_message.Header.DataSchema, message.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, message.Header.Subject);
        Assert.Equal(_message.Body.Bytes, message.Body.Bytes);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_json_format_and_provided_message_has_no_body()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null,  _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody([]) );
        
        var cloudEventsMessage = _transformer.Wrap(_message, new Publication
        {
            DataSchema = _dataSchema,
            Source = _source,
            Type = _type,
            Subject = _subject
        });
       
        // act
        var unwrap = _transformer.Unwrap(cloudEventsMessage);
       
        Assert.Equal(_message.Header.Source, unwrap.Header.Source);
        Assert.Equal(_message.Header.Type, unwrap.Header.Type);
        Assert.Equal(new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()}, unwrap.Header.ContentType);
        Assert.Equal(_message.Header.DataSchema, unwrap.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, unwrap.Header.Subject);
        Assert.Equal([], unwrap.Body.Bytes);
    }
    
    [Fact]
    public void When_unwrap_a_message_with_json_format()
    {
        //arrange
        _transformer.InitializeWrapFromAttributeParams(_source.AbsoluteUri, _type, null, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var body = _message.Body;
        var cloudEventsMessage = _transformer.Wrap(_message, new Publication
        {
            CloudEventsAdditionalProperties = new Dictionary<string, object>
            {
                ["test"] = "test-header"
            }
        });
       
        // act
        var unwrap = _transformer.Unwrap(cloudEventsMessage);
       
        Assert.Equal(_message.Header.Source, unwrap.Header.Source);
        Assert.Equal(_message.Header.Type, unwrap.Header.Type);
        Assert.Equal(new ContentType(MediaTypeNames.Text.Plain), unwrap.Header.ContentType);
        Assert.Equal(_message.Header.DataSchema, unwrap.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, unwrap.Header.Subject);
        Assert.Equal(body.Bytes, unwrap.Body.Bytes);
        Assert.Single(unwrap.Header.Bag);
        Assert.Equal("test-header", unwrap.Header.Bag["test"]);
    }
}
