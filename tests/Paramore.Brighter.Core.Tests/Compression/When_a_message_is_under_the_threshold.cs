using System;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class SmallPayloadNotCompressedTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly string _body;
    private readonly Message _message;
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;


    public SmallPayloadNotCompressedTests()
    {
        _transformer = new CompressPayloadTransformer();
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);

        _body = "small message";
        _message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow, contentType: MessageBody.APPLICATION_JSON),
            new MessageBody(_body, MessageBody.APPLICATION_JSON, CharacterEncoding.UTF8));      
    }


    [Fact]
    public async Task When_a_message_is_under_the_threshold()
    {
        var uncompressedMessage = await _transformer.WrapAsync(_message);

        //look for gzip in the bytes
        uncompressedMessage.Body.ContentType.Should().Be(MessageBody.APPLICATION_JSON);
        uncompressedMessage.Body.Value.Should().Be(_message.Body.Value);
    }
}
