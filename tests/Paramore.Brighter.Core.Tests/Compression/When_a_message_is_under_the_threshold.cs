using System;
using System.IO.Compression;
using System.Net.Mime;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class SmallPayloadNotCompressedTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");

    public SmallPayloadNotCompressedTests()
    {
        _transformer = new CompressPayloadTransformer();
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);

        string body = "small message";
        var contentType = new ContentType(MediaTypeNames.Application.Json);
        _message = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT,
                timeStamp: DateTime.UtcNow, contentType: contentType),
            new MessageBody(body, contentType, CharacterEncoding.UTF8)
        );
    }

    [Fact]
    public void When_a_message_is_under_the_threshold()
    {
        var uncompressedMessage = _transformer.Wrap(_message, new Publication{Topic = _topic});

        //look for gzip in the bytes
        Assert.Equal(
            new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()}, 
            uncompressedMessage.Body.ContentType);
        Assert.Equal(_message.Body.Value, uncompressedMessage.Body.Value);
    }
}
