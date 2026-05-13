using System;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Attributes;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class When_wrapping_a_message_that_already_has_cloud_event_headers_should_preserve_them
{
    private readonly CloudEventsTransformer _transformer = new();
    private readonly Uri _mapperSource = new("http://goparamore.io/MyMapper");
    private readonly CloudEventsType _mapperType = new("MyApp.OrderAccepted");
    private readonly Uri _mapperDataSchema = new("http://goparamore.io/MyMapper/schema");
    private readonly string _mapperSubject = "OrderAccepted";

    private Message CreateMessageWithMapperHeaders()
    {
        return new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                contentType: new ContentType(MediaTypeNames.Application.Json),
                source: _mapperSource,
                subject: _mapperSubject,
                type: _mapperType,
                dataSchema: _mapperDataSchema),
            new MessageBody("{\"orderId\": 123}"));
    }

    [Fact]
    public void When_no_attribute_and_no_publication_should_preserve_mapper_headers()
    {
        //Arrange - a message with cloud event headers already set (e.g. by a message mapper)
        var message = CreateMessageWithMapperHeaders();

        // Publication has no cloud event properties set - the mapper already handled them
        var publication = new Publication();

        //Act - CloudEventsTransformer wraps after the mapper
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - existing header values should be preserved, not overwritten
        Assert.Equal(_mapperSource, wrapped.Header.Source);
        Assert.Equal(_mapperType, wrapped.Header.Type);
        Assert.Equal(_mapperDataSchema, wrapped.Header.DataSchema);
        Assert.Equal(_mapperSubject, wrapped.Header.Subject);
    }

    [Fact]
    public void When_attribute_is_set_should_override_mapper_headers()
    {
        //Arrange
        var message = CreateMessageWithMapperHeaders();
        const string attrSource = "http://goparamore.io/FromAttribute";
        const string attrType = "MyApp.FromAttribute";
        const string attrDataContentType = MediaTypeNames.Text.Xml;
        const string attrDataSchema = "http://goparamore.io/FromAttribute/schema";
        const string attrSubject = "FromAttribute";

        _transformer.InitializeWrapFromAttributeParams(attrSource, attrType, null, attrDataContentType, attrDataSchema, attrSubject, CloudEventFormat.Binary);

        var publication = new Publication
        {
            Source = new Uri("http://goparamore.io/FromPublication"),
            Type = new CloudEventsType("MyApp.FromPublication"),
            DataSchema = new Uri("http://goparamore.io/FromPublication/schema"),
            Subject = "FromPublication"
        };

        //Act
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - attribute wins over both mapper and publication
        Assert.Equal(attrSource, wrapped.Header.Source.ToString());
        Assert.Equal(attrType, wrapped.Header.Type);
        Assert.Equal(new ContentType(attrDataContentType), wrapped.Header.ContentType);
        Assert.Equal(attrDataSchema, wrapped.Header.DataSchema!.ToString());
        Assert.Equal(attrSubject, wrapped.Header.Subject);
    }

    [Fact]
    public void When_mapper_sets_headers_and_publication_also_sets_them_mapper_should_win()
    {
        //Arrange - mapper set headers, publication has different values
        var message = CreateMessageWithMapperHeaders();

        var publication = new Publication
        {
            Source = new Uri("http://goparamore.io/FromPublication"),
            Type = new CloudEventsType("MyApp.FromPublication"),
            DataSchema = new Uri("http://goparamore.io/FromPublication/schema"),
            Subject = "FromPublication"
        };

        //Act - no attribute params, so precedence is mapper > publication
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - mapper values win over publication
        Assert.Equal(_mapperSource, wrapped.Header.Source);
        Assert.Equal(_mapperType, wrapped.Header.Type);
        Assert.Equal(_mapperDataSchema, wrapped.Header.DataSchema);
        Assert.Equal(_mapperSubject, wrapped.Header.Subject);
    }

    [Fact]
    public void When_mapper_leaves_defaults_publication_should_provide_values()
    {
        //Arrange - message with default headers (mapper did not set cloud event properties)
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("Test.Topic"), MessageType.MT_EVENT),
            new MessageBody("{\"orderId\": 123}"));

        var publicationSource = new Uri("http://goparamore.io/FromPublication");
        var publicationType = new CloudEventsType("MyApp.FromPublication");
        var publicationDataSchema = new Uri("http://goparamore.io/FromPublication/schema");
        const string publicationSubject = "FromPublication";

        var publication = new Publication
        {
            Source = publicationSource,
            Type = publicationType,
            DataSchema = publicationDataSchema,
            Subject = publicationSubject
        };

        //Act
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - publication values are used as fallback when message has defaults
        Assert.Equal(publicationSource, wrapped.Header.Source);
        Assert.Equal(publicationType, wrapped.Header.Type);
        Assert.Equal(publicationDataSchema, wrapped.Header.DataSchema);
        Assert.Equal(publicationSubject, wrapped.Header.Subject);
    }

    [Fact]
    public void When_mapper_sets_relative_uri_source_should_preserve_it()
    {
        //Arrange - CloudEvents spec allows relative URI-references for source
        var relativeSource = new Uri("/my-service/orders", UriKind.Relative);
        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                source: relativeSource),
            new MessageBody("{\"orderId\": 123}"));

        var publication = new Publication();

        //Act - should not throw, and should preserve the relative URI
        var wrapped = _transformer.Wrap(message, publication);

        //Assert
        Assert.Equal(relativeSource, wrapped.Header.Source);
    }

    [Fact]
    public void When_wrapping_as_json_format_should_preserve_mapper_headers_in_body()
    {
        //Arrange
        var message = CreateMessageWithMapperHeaders();
        message.Header.ContentType = new ContentType(MediaTypeNames.Application.Json);

        _transformer.InitializeWrapFromAttributeParams(null, null, null, null, null, null, CloudEventFormat.Json);

        var publication = new Publication();

        //Act
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - the CloudEvent JSON body should contain the mapper's values
        var json = JsonSerializer.Deserialize<CloudEventsTransformer.JsonEvent>(wrapped.Body.Bytes, JsonSerialisationOptions.Options);
        Assert.NotNull(json);
        Assert.Equal(_mapperSource, json.Source);
        Assert.Equal(_mapperType, json.Type);
        Assert.Equal(_mapperDataSchema, json.DataSchema);
        Assert.Equal(_mapperSubject, json.Subject);
    }

    [Fact]
    public void When_attribute_has_default_data_content_type_should_not_override_mapper_content_type()
    {
        //Arrange - mapper sets XML content type; attribute uses [CloudEvents(0)] with no explicit DataContentType
        var xmlContentType = new ContentType(MediaTypeNames.Text.Xml);
        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                contentType: xmlContentType,
                source: _mapperSource,
                subject: _mapperSubject,
                type: _mapperType,
                dataSchema: _mapperDataSchema),
            new MessageBody("<order><id>123</id></order>"));

        // Simulate the real attribute pipeline: CloudEventsAttribute with no explicit DataContentType
        var attribute = new CloudEventsAttribute(0);
        var initParams = attribute.InitializerParams();
        _transformer.InitializeWrapFromAttributeParams(initParams);

        var publication = new Publication();

        //Act
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - mapper's XML content type should be preserved, not overwritten with application/json
        Assert.Equal(xmlContentType, wrapped.Header.ContentType);
    }

    [Fact]
    public void When_mapper_sets_source_to_default_uri_publication_wins_sentinel_collision()
    {
        //Arrange - mapper explicitly sets Source to the same URI as DefaultSource (http://goparamore.io).
        // This is a known trade-off: we use the default URI as a sentinel for "mapper didn't set it,"
        // so a mapper that intentionally picks the default URI will have its value replaced by publication.
        var defaultSource = new Uri(MessageHeader.DefaultSource);
        var publicationSource = new Uri("http://goparamore.io/FromPublication");

        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                source: defaultSource),
            new MessageBody("{\"orderId\": 123}"));

        var publication = new Publication
        {
            Source = publicationSource
        };

        //Act
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - publication wins because the mapper's value matches the sentinel
        Assert.Equal(publicationSource, wrapped.Header.Source);
    }
}
