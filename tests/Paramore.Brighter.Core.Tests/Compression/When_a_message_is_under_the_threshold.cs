using System;
using System.IO.Compression;
using System.Net.Mime;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;

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
        _message = new Message(new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: contentType), new MessageBody(body, contentType, CharacterEncoding.UTF8));
    }

    [Test]
    public async Task When_a_message_is_under_the_threshold()
    {
        var uncompressedMessage = await _transformer.WrapAsync(_message, new Publication { Topic = _topic });
        //look for gzip in the bytes
        await Assert.That(uncompressedMessage.Body.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
        await Assert.That(uncompressedMessage.Body.Value).IsEqualTo(_message.Body.Value);
    }
}