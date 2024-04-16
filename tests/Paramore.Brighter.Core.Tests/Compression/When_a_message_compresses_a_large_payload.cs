using System;
using System.IO.Compression;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressLargePayloadTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly string _body;
    private readonly Message _message;
    private string _topic;
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;
    private const byte ZLIB_LEAD_BYTE = 0x78;

    public CompressLargePayloadTests()
    {
        _transformer = new CompressPayloadTransformer();
        
        _body = DataGenerator.CreateString(6000);
        _topic = "test_topic";
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, DateTime.UtcNow),
            new MessageBody(_body, MessageBody.APPLICATION_JSON, CharacterEncoding.UTF8));        
    }

    [Fact]
    public void When_a_message_gzip_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        compressedMessage.Body.Bytes.Should().NotBeNull();
        compressedMessage.Body.Bytes.Length.Should().BeGreaterThanOrEqualTo(2);
        BitConverter.ToUInt16(compressedMessage.Body.Bytes, 0).Should().Be(GZIP_LEAD_BYTES);
        
        //mime types
        compressedMessage.Header.ContentType.Should().Be(CompressPayloadTransformerAsync.GZIP);
        compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER].Should().Be(MessageBody.APPLICATION_JSON);
        compressedMessage.Body.ContentType.Should().Be(CompressPayloadTransformerAsync.GZIP);
        

    }
    
    [Fact]
    public void When_a_message_zlib_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Zlib, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{ Topic = new RoutingKey(_topic)});
    
        //look for gzip in the bytes
        compressedMessage.Body.Bytes.Should().NotBeNull();
        compressedMessage.Body.Bytes.Length.Should().BeGreaterThanOrEqualTo(2);
        compressedMessage.Body.ContentType.Should().Be("application/deflate");
        compressedMessage.Body.Bytes[0].Should().Be(ZLIB_LEAD_BYTE);
    
        //mime types
        compressedMessage.Header.ContentType.Should().Be(CompressPayloadTransformerAsync.DEFLATE);
        compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER].Should().Be(MessageBody.APPLICATION_JSON);
        compressedMessage.Body.ContentType.Should().Be(CompressPayloadTransformerAsync.DEFLATE);
    }
    
    [Fact]
    public void When_a_message_brotli_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Brotli, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{Topic = new RoutingKey(_topic)});
    
        //look for gzip in the bytes
        compressedMessage.Body.Bytes.Should().NotBeNull();
        compressedMessage.Body.Bytes.Length.Should().BeGreaterThanOrEqualTo(2);
        compressedMessage.Body.ContentType.Should().Be("application/br");
        
        //mime types
        compressedMessage.Header.ContentType.Should().Be(CompressPayloadTransformerAsync.BROTLI);
        compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER].Should().Be(MessageBody.APPLICATION_JSON);
        compressedMessage.Body.ContentType.Should().Be(CompressPayloadTransformerAsync.BROTLI);
    
    }
}
