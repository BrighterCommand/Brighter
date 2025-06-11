using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class AsyncSmallPayloadNotCompressedTests
{
    private readonly CompressPayloadTransformerAsync _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;

    public AsyncSmallPayloadNotCompressedTests()
    {
        _transformer = new CompressPayloadTransformerAsync();
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);

        string body = "small message";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT,
                timeStamp: DateTime.UtcNow, contentType: new ContentType(MediaTypeNames.Application.Json)
            ),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8)
        );
    }

    [Fact]
    public async Task When_a_message_is_under_the_threshold()
    {
        var uncompressedMessage = await _transformer.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.Equal(new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()}, uncompressedMessage.Body.ContentType);
        Assert.Equal(_message.Body.Value, uncompressedMessage.Body.Value);
    }
}
