using System;
using System.IO.Compression;
using System.Net.Mime;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressLargePayloadTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;
    private const byte ZLIB_LEAD_BYTE = 0x78;

    public CompressLargePayloadTests()
    {
        _transformer = new CompressPayloadTransformer();

        string body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
    }

    [Fact]
    public void When_a_message_gzip_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(GZIP_LEAD_BYTES, BitConverter.ToUInt16(compressedMessage.Body.Bytes, 0));

        //mime types
        Assert.Equal(
            new ContentType("application/gzip"), 
            compressedMessage.Header.ContentType);
        Assert.Equal(
            new ContentType(MediaTypeNames.Application.Json){ CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }, 
            compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(
            new ContentType("application/gzip"), 
            compressedMessage.Body.ContentType);
    }

    [Fact]
    public void When_a_message_zlib_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Zlib, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{ Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(
            new ContentType("application/deflate"), 
            compressedMessage.Body.ContentType);
        Assert.Equal(ZLIB_LEAD_BYTE, compressedMessage.Body.Bytes[0]);

        //mime types
        Assert.Equal(
            new ContentType(CompressPayloadTransformer.DEFLATE), 
            compressedMessage.Header.ContentType
            );
        Assert.Equal(
            new ContentType(MediaTypeNames.Application.Json){ CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }, 
            compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(
            new ContentType(CompressPayloadTransformer.DEFLATE), 
            compressedMessage.Body.ContentType);
    }

    [Fact]
    public void When_a_message_brotli_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Brotli, CompressionLevel.Optimal, 5);
        var compressedMessage = _transformer.Wrap(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(
            new ContentType("application/br"), 
            compressedMessage.Body.ContentType);

        //mime types
        Assert.Equal(
            new ContentType(CompressPayloadTransformer.BROTLI), 
            compressedMessage.Header.ContentType);
        Assert.Equal(
            new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()}.ToString(), 
            compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(
            new ContentType(CompressPayloadTransformer.BROTLI), 
            compressedMessage.Body.ContentType);
    }
}
