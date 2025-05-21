using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class AsyncSmallPayloadNotCompressedTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;

    public AsyncSmallPayloadNotCompressedTests()
    {
        _transformer = new CompressPayloadTransformer();
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);

        string body = "small message";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT,
                timeStamp: DateTime.UtcNow, contentType: MessageBody.APPLICATION_JSON
            ),
            new MessageBody(body, MessageBody.APPLICATION_JSON, CharacterEncoding.UTF8)
        );
    }

    [Fact]
    public async Task When_a_message_is_under_the_threshold()
    {
        var uncompressedMessage = await _transformer.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.Equal(MessageBody.APPLICATION_JSON, uncompressedMessage.Body.ContentType);
        Assert.Equal(_message.Body.Value, uncompressedMessage.Body.Value);
    }
}
