using System;
using System.Net.Mime;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CloudEvents;

public class CloudEventsPreservesExistingHeadersTests
{
    private readonly CloudEventsTransformer _transformer = new();
    private readonly Uri _source = new("http://goparamore.io/MyMapper");
    private readonly CloudEventsType _type = new("MyApp.OrderAccepted");
    private readonly Uri _dataSchema = new("http://goparamore.io/MyMapper/schema");
    private readonly string _subject = "OrderAccepted";

    [Fact]
    public void When_wrapping_a_message_that_already_has_cloud_event_headers_should_preserve_them()
    {
        //Arrange - a message with cloud event headers already set (e.g. by a message mapper)
        var message = new Message(
            new MessageHeader(
                Id.Random(),
                new RoutingKey("Test.Topic"),
                MessageType.MT_EVENT,
                contentType: new ContentType(MediaTypeNames.Application.Json),
                source: _source,
                subject: _subject,
                type: _type,
                dataSchema: _dataSchema),
            new MessageBody("{\"orderId\": 123}"));

        // Publication has no cloud event properties set — the mapper already handled them
        var publication = new Publication();

        //Act - CloudEventsTransformer wraps after the mapper
        var wrapped = _transformer.Wrap(message, publication);

        //Assert - existing header values should be preserved, not overwritten
        Assert.Equal(_source, wrapped.Header.Source);
        Assert.Equal(_type, wrapped.Header.Type);
        Assert.Equal(_dataSchema, wrapped.Header.DataSchema);
        Assert.Equal(_subject, wrapped.Header.Subject);
    }
}
