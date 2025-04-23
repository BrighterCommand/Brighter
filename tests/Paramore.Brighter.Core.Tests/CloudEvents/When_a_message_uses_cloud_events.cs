using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsTransformerTests 
{
    private readonly CloudEventsTransformer _transformer;
    private Uri _source;
    private string _type;
    private string _dataContentType;
    private Uri _dataSchema;
    private string _subject;
    private Message _message;

    public CloudEventsTransformerTests()
    {
        _transformer = new CloudEventsTransformer();
        _source = new Uri("http://goparamore.io/CloudEventsTransformerTests");
        _type = typeof(MyCommand).FullName;
        _dataContentType = "application/json";
        _dataSchema = new Uri("http://goparamore.io/CloudEventsTransformerTests/schema");
        _subject = "CloudEventsTransformerTests";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody("{\"content\": \"test\"}")
        );
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_attribute_and_format_as_binary()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.ToString(), _type, null, _dataContentType, _dataSchema.ToString(), _subject, CloudEventFormat.Binary);
        var cloudEvents = _transformer.Wrap(_message, new Publication());
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_dataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.Equal(body.Bytes, cloudEvents.Body.Bytes);
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_attribute_and_format_as_json()
    {
        //act
        var body = _message.Body;
        _transformer.InitializeWrapFromAttributeParams(_source.ToString(), _type, null, _dataContentType, _dataSchema.ToString(), _subject, CloudEventFormat.Json);
        var cloudEvents = _transformer.Wrap(_message, new Publication());
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal("application/cloudevents+json", cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);
        Assert.NotEqual(body.Bytes, cloudEvents.Body.Bytes);

        var json = JsonSerializer.Deserialize<CloudEventsTransformer.CloudEventMessage>(cloudEvents.Body.Bytes);
        Assert.NotNull(json);
        Assert.Equal(_source, json.Source);
        Assert.Equal(_type, json.Type);
        Assert.Equal(_dataContentType, json.DataContentType);
        Assert.Equal(_dataSchema, json.DataSchema);
        Assert.Equal(_subject, json.Subject);
        Assert.True(JsonElement.DeepEquals(json.Data, JsonSerializer.Deserialize<JsonElement>(body.Bytes)));
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_a_publication()
    {
        //act
        var cloudEvents = _transformer.Wrap(_message, new Publication
        {
            Source = _source,
            Type = _type,
            ContentType = _dataContentType,
            DataSchema = _dataSchema,
            Subject = _subject
        });
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_dataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
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
            ContentType = _dataContentType,
            DataSchema = _dataSchema,
            Subject = _subject
        };
        
        //These attributes override the publication values above
        const string source = "http://goparamore.io/OverrideSource";
        const string type = "goparamore.io/OverrideType";
        const string dataContentType = "application/xml";
        const string dataSchema = "http://goparamore.io/CloudEventsTransformerOverride/schema";
        const string subject = "CloudEventsTransformerAlternative"; 

        //act
        _transformer.InitializeWrapFromAttributeParams(source, type, null, dataContentType, dataSchema, subject, CloudEventFormat.Binary);

        var cloudEvents = _transformer.Wrap(_message, publication);
        
        //assert
        Assert.Equal(source, cloudEvents.Header.Source.ToString());
        Assert.Equal(type, cloudEvents.Header.Type);
        Assert.Equal(dataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(dataSchema, cloudEvents.Header.DataSchema.ToString());
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
            ContentType = _dataContentType,
            DataSchema = _dataSchema,
            Subject = _subject
        };
        
        //These attributes override the publication values above
        const string source = "http://goparamore.io/OverrideSource";
        const string type = "goparamore.io/OverrideType";
        const string dataContentType = "application/json";
        const string cloudEventDataContentType = "application/cloudevents+json";
        const string dataSchema = "http://goparamore.io/CloudEventsTransformerOverride/schema";
        const string subject = "CloudEventsTransformerAlternative"; 
        var body = _message.Body;

        //act
        _transformer.InitializeWrapFromAttributeParams(source, type, null, dataContentType, dataSchema, subject, CloudEventFormat.Json);

        var cloudEvents = _transformer.Wrap(_message, publication);
        
        //assert
        Assert.Equal(source, cloudEvents.Header.Source.ToString());
        Assert.Equal(type, cloudEvents.Header.Type);
        Assert.Equal(cloudEventDataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(dataSchema, cloudEvents.Header.DataSchema!.ToString());
        Assert.Equal(subject, cloudEvents.Header.Subject);
        Assert.NotEqual(body.Bytes, cloudEvents.Body.Bytes);
        
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.CloudEventMessage>(cloudEvents.Body.Bytes);
        Assert.NotNull(json);
        Assert.Equal(source, json.Source.ToString());
        Assert.Equal(type, json.Type);
        Assert.Equal(dataContentType, json.DataContentType);
        Assert.Equal(dataSchema, json.DataSchema!.ToString());
        Assert.Equal(subject, json.Subject);
        Assert.True(JsonElement.DeepEquals(json.Data, JsonSerializer.Deserialize<JsonElement>(body.Bytes)));
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
        Assert.Equal("text/plain", cloudEvents.Header.ContentType);
        Assert.Null(cloudEvents.Header.DataSchema);
        Assert.Null(cloudEvents.Header.Subject);
    }
}
