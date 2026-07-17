using System;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Compression;
public class AsyncUncompressedPayloadTests
{
    [Test]
    public async Task When_a_message_is_not_gzip_compressed()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        var smallContent = "small message";
        var contentType = new ContentType(MediaTypeNames.Application.Json);
        var body = new MessageBody(smallContent, contentType);
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: contentType), body);
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(smallContent);
    }

    [Test]
    public async Task When_a_message_is_not_zlib_compressed()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib);
        var smallContent = "small message";
        var contentType = new ContentType(MediaTypeNames.Application.Json);
        var body = new MessageBody(smallContent, contentType);
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: contentType), body);
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(smallContent);
    }

    [Test]
    public async Task When_a_message_is_not_brotli_compressed()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli);
        var smallContent = "small message";
        var contentType = new ContentType(MediaTypeNames.Application.Json);
        var body = new MessageBody(smallContent, contentType);
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, timeStamp: DateTime.UtcNow, contentType: contentType), body);
        //act
        var msg = await transformer.UnwrapAsync(message);
        //assert
        await Assert.That(msg.Body.Value).IsEqualTo(smallContent);
    }
}