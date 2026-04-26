using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Compression;
public class AsyncCompressLargePayloadTests
{
    private readonly CompressPayloadTransformer _transformer;
    private readonly Message _message;
    private readonly RoutingKey _topic = new("test_topic");
    private const ushort GZIP_LEAD_BYTES = 0x8b1f;
    private const byte ZLIB_LEAD_BYTE = 0x78;
    public AsyncCompressLargePayloadTests()
    {
        _transformer = new CompressPayloadTransformer();
        string body = DataGenerator.CreateString(6000);
        _message = new Message(new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow), new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
    }

    [Test]
    public async Task When_a_message_gzip_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication { Topic = new RoutingKey(_topic) });
        //look for gzip in the bytes
        await Assert.That(compressedMessage.Body.Bytes).IsNotNull();
        await Assert.That(compressedMessage.Body.Bytes.Length >= 2).IsTrue();
        await Assert.That(BitConverter.ToUInt16(compressedMessage.Body.Bytes, 0)).IsEqualTo(GZIP_LEAD_BYTES);
        //mime types
        await Assert.That(compressedMessage.Header.ContentType).IsEqualTo(new ContentType("application/gzip"));
        await Assert.That(compressedMessage.Body.ContentType).IsEqualTo(new ContentType("application/gzip"));
        await Assert.That(compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }.ToString());
    }

    [Test]
    public async Task When_a_message_zlib_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Zlib, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication { Topic = new RoutingKey(_topic) });
        //look for gzip in the bytes
        await Assert.That(compressedMessage.Body.Bytes).IsNotNull();
        await Assert.That(compressedMessage.Body.Bytes.Length >= 2).IsTrue();
        await Assert.That(compressedMessage.Body.ContentType!.MediaType).IsEqualTo(new ContentType("application/deflate").MediaType);
        await Assert.That(compressedMessage.Body.Bytes[0]).IsEqualTo(ZLIB_LEAD_BYTE);
        //mime types
        await Assert.That(compressedMessage.Header.ContentType).IsEqualTo(new ContentType(CompressPayloadTransformer.DEFLATE));
        await Assert.That(compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }.ToString());
        await Assert.That(compressedMessage.Body.ContentType).IsEqualTo(new ContentType(CompressPayloadTransformer.DEFLATE));
    }

    [Test]
    public async Task When_a_message_brotli_compresses_a_large_payload()
    {
        _transformer.InitializeWrapFromAttributeParams(CompressionMethod.Brotli, CompressionLevel.Optimal, 5);
        var compressedMessage = await _transformer.WrapAsync(_message, new Publication { Topic = new RoutingKey(_topic) });
        //look for gzip in the bytes
        await Assert.That(compressedMessage.Body.Bytes).IsNotNull();
        await Assert.That(compressedMessage.Body.Bytes.Length >= 2).IsTrue();
        //mime types
        var contentType = new ContentType("application/br");
        await Assert.That(compressedMessage.Body.ContentType!).IsEqualTo(contentType);
        await Assert.That(compressedMessage.Header.ContentType!).IsEqualTo(contentType);
        await Assert.That(compressedMessage.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }.ToString());
    }
}