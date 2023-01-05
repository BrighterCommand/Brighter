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

public class UncompressLargePayloadTests
{
    [Fact]
    public async Task When_decompressing_a_large_gzip_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip, "application/json");
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new GZipStream(output, CompressionLevel.Optimal);
            
        string mimeType = "application/gzip";
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);

    }
    
    [Fact]
    public async Task When_decompressing_a_large_zlib_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib, "application/json");
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new ZLibStream(output, CompressionLevel.Optimal);
            
        string mimeType = "application/deflate";
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);

    }
    
    [Fact]
    public async Task When_decompressing_a_large_brotli_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli, "application/json");
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new BrotliStream(output, CompressionLevel.Optimal);
            
        string mimeType = "application/br";
        await input.CopyToAsync(compressionStream);
        await compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_EVENT, DateTime.UtcNow),body);
        
        //act
        var msg = await transformer.UnwrapAsync(message);
        
        //assert
        msg.Body.Value.Should().Be(largeContent);

    }
}
