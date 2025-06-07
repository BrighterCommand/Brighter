using System;
using System.Net.Mime;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsTransformerTests 
{
    private readonly CloudEventsTransformer _transformer;
    private readonly Uri _source;
    private readonly string _type;
    private readonly ContentType _dataContentType;
    private readonly Uri _dataSchema;
    private readonly string _subject;
    private readonly Message _message;

    public CloudEventsTransformerTests()
    {
        _transformer = new CloudEventsTransformer();
        _source = new Uri("http://goparamore.io/CloudEventsTransformerTests");
        _type = typeof(MyCommand).FullName;
        _dataContentType = new(MediaTypeNames.Application.Json);
        _dataSchema = new Uri("http://goparamore.io/CloudEventsTransformerTests/schema");
        _subject = "CloudEventsTransformerTests";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("Test Topic"), MessageType.MT_COMMAND), 
            new MessageBody("test content")
        );
    }
    
    [Fact]
    public void When_a_message_uses_cloud_events_via_attribute()
    {
        //act
        _transformer.InitializeWrapFromAttributeParams(_source.ToString(), _type, null, _dataContentType, _dataSchema.ToString(), _subject);
        var cloudEvents = _transformer.Wrap(_message, new Publication());
        
        //assert
        Assert.Equal(_source, cloudEvents.Header.Source);
        Assert.Equal(_type, cloudEvents.Header.Type);
        Assert.Equal(_dataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(_dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(_subject, cloudEvents.Header.Subject);

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
    public void When_a_message_uses_cloud_events_and_a_publication()
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
        ContentType dataContentType = new ContentType(MediaTypeNames.Application.Xml);
        Uri dataSchema = new Uri("http://goparamore.io/CloudEventsTransformerOverride/schema");
        const string subject = "CloudEventsTransformerAlternative"; 

        //act
        _transformer.InitializeWrapFromAttributeParams(source, type, null, dataContentType, dataSchema, subject);

        var cloudEvents = _transformer.Wrap(_message, publication);
        
        //assert
        Assert.Equal(source, cloudEvents.Header.Source.ToString());
        Assert.Equal(type, cloudEvents.Header.Type);
        Assert.Equal(dataContentType, cloudEvents.Header.ContentType);
        Assert.Equal(dataSchema, cloudEvents.Header.DataSchema);
        Assert.Equal(subject, cloudEvents.Header.Subject);

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
}
