using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class AsyncCompressLargePayloadTests
{
    private readonly CompressPayloadTransformerAsync _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;
    private const byte ZLIB_LEAD_BYTE = 0x78;

    public AsyncCompressLargePayloadTests()
    {
        _transformer = new CompressPayloadTransformerAsync();

        string body = DataGenerator.CreateString(6000);
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
    }

    [Fact]
    public async Task When_a_message_gzip_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(GZIP_LEAD_BYTES, BitConverter.ToUInt16(compressedMessage.Body.Bytes, 0));

        //mime types
        Assert.Equal(new ContentType(MediaTypeNames.Application.GZip), compressedMessage.Header.ContentType);
        Assert.Equal(MediaTypeNames.Application.Json, compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(new ContentType(MediaTypeNames.Application.GZip), compressedMessage.Body.ContentType);
    }

    [Fact]
    public async Task When_a_message_zlib_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Zlib, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(new ContentType("application/deflate"), compressedMessage.Body.ContentType);
        Assert.Equal(ZLIB_LEAD_BYTE, compressedMessage.Body.Bytes[0]);

        //mime types
        Assert.Equal(new ContentType(CompressPayloadTransformerAsync.DEFLATE), compressedMessage.Header.ContentType);
        Assert.Equal(MediaTypeNames.Application.Json, compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(new ContentType(CompressPayloadTransformerAsync.DEFLATE), compressedMessage.Body.ContentType);
    }

    [Fact]
    public async Task When_a_message_brotli_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Brotli, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication{Topic = new RoutingKey(_topic)});

        //look for gzip in the bytes
        Assert.NotNull(compressedMessage.Body.Bytes);
        Assert.True(compressedMessage.Body.Bytes.Length >= 2);
        Assert.Equal(new ContentType("iapplication/br"), compressedMessage.Body.ContentType);

        //mime types
        Assert.Equal(new ContentType(CompressPayloadTransformerAsync.BROTLI), compressedMessage.Header.ContentType);
        Assert.Equal(new ContentType(MediaTypeNames.Application.Json), compressedMessage.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal(new ContentType(CompressPayloadTransformerAsync.BROTLI), compressedMessage.Body.ContentType);
    }
}
