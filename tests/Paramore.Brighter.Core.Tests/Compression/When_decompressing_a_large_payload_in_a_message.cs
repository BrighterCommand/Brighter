using System;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Compression;
public class UncompressLargePayloadTests
{
    [Test]
    public async Task When_decompressing_a_large_gzip_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        var largeContent = DataGenerator.CreateString(6000);
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();
        Stream compressionStream = new GZipStream(output, CompressionLevel.Optimal);
        string mimeType = CompressPayloadTransformer.GZIP;
        input.CopyToAsync(compressionStream);
        compressionStream.FlushAsync();
        var body = new MessageBody(output.ToArray(), new ContentType(mimeType));
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: new ContentType(mimeType)), body);
        message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER] = MediaTypeNames.Application.Json;
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(largeContent);
        await Assert.That(msg.Body.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
        await Assert.That(msg.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
    }

    [Test]
    public async Task When_decompressing_a_large_zlib_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib);
        var largeContent = DataGenerator.CreateString(6000);
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();
        Stream compressionStream = new ZLibStream(output, CompressionLevel.Optimal);
        string mimeType = CompressPayloadTransformer.DEFLATE;
        input.CopyToAsync(compressionStream);
        compressionStream.FlushAsync();
        var body = new MessageBody(output.ToArray(), new ContentType(mimeType));
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: new ContentType(mimeType)), body);
        message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER] = MediaTypeNames.Application.Json;
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(largeContent);
        await Assert.That(msg.Body.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
        await Assert.That(msg.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
    }

    [Test]
    public async Task When_decompressing_a_large_brotli_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli);
        var largeContent = DataGenerator.CreateString(6000);
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();
        Stream compressionStream = new BrotliStream(output, CompressionLevel.Optimal);
        string mimeType = CompressPayloadTransformer.BROTLI;
        input.CopyToAsync(compressionStream);
        compressionStream.FlushAsync();
        var body = new MessageBody(output.ToArray(), new ContentType(mimeType));
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: new ContentType(mimeType)), body);
        message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER] = MediaTypeNames.Application.Json;
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(largeContent);
        await Assert.That(msg.Body.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
        await Assert.That(msg.Header.ContentType).IsEqualTo(new ContentType(MediaTypeNames.Application.Json) { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() });
    }
}