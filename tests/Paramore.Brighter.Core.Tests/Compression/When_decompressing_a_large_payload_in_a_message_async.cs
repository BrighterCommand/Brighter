using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class AsyncUncompressLargePayloadTests
{
    [Fact]
    public async Task When_decompressing_a_large_gzip_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformerAsync();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new GZipStream(output, CompressionLevel.Optimal);
            
        string mimeType = CompressPayloadTransformerAsync.GZIP;
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, 
                timeStamp:DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);
        msg.Body.ContentType.Should().Be(MessageBody.APPLICATION_JSON);
        msg.Header.ContentType.Should().Be(MessageBody.APPLICATION_JSON);

    }
    
    [Fact]
    public async Task When_decompressing_a_large_zlib_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformerAsync();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib);
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new ZLibStream(output, CompressionLevel.Optimal);
            
        string mimeType = CompressPayloadTransformerAsync.DEFLATE;
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
         //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);
        msg.Body.ContentType.Should().Be(MessageBody.APPLICATION_JSON);
        msg.Header.ContentType.Should().Be(MessageBody.APPLICATION_JSON);

    }
    
    [Fact]
    public async Task When_decompressing_a_large_brotli_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformerAsync();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli);
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new BrotliStream(output, CompressionLevel.Optimal);
            
        string mimeType = CompressPayloadTransformerAsync.BROTLI;
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "test_topic", MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
        //act
         var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);
        msg.Body.ContentType.Should().Be(MessageBody.APPLICATION_JSON);
        msg.Header.ContentType.Should().Be(MessageBody.APPLICATION_JSON);

    }
}
