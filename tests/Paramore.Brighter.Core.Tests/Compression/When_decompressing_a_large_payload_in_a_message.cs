using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class UncompressLargePayloadTests
{
    [Fact]
    public void When_decompressing_a_large_gzip_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new GZipStream(output, CompressionLevel.Optimal);
            
        string mimeType = CompressPayloadTransformerAsync.GZIP;
        input.CopyToAsync(compressionStream);
        compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
                ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
        //act
        var msg = transformer.Unwrap(message);
        
        //assert
        Assert.Equal(largeContent, msg.Body.Value);
        Assert.Equal(MessageBody.APPLICATION_JSON, msg.Body.ContentType);
        Assert.Equal(MessageBody.APPLICATION_JSON, msg.Header.ContentType);
    }
    
    [Fact]
    public void When_decompressing_a_large_zlib_payload_in_a_message()
    {
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib);
        
        var largeContent = DataGenerator.CreateString(6000);
        
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(largeContent));
        using var output = new MemoryStream();

        Stream compressionStream = new ZLibStream(output, CompressionLevel.Optimal);
            
        string mimeType = CompressPayloadTransformerAsync.DEFLATE;
        input.CopyToAsync(compressionStream);
        compressionStream.FlushAsync();

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, 
                timeStamp:DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
         //act
        var msg = transformer.Unwrap(message);
        
        //assert
        Assert.Equal(largeContent, msg.Body.Value);
        Assert.Equal(MessageBody.APPLICATION_JSON, msg.Body.ContentType);
        Assert.Equal(MessageBody.APPLICATION_JSON, msg.Header.ContentType);
    }
    
    [Fact]
    public void When_decompressing_a_large_brotli_payload_in_a_message()
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

        var body = new MessageBody(output.ToArray(), mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test_topic"), MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        message.Header.Bag[CompressPayloadTransformerAsync.ORIGINAL_CONTENTTYPE_HEADER] = MessageBody.APPLICATION_JSON;
        
        //act
         var msg = transformer.Unwrap(message);

         //assert
         Assert.Equal(largeContent, msg.Body.Value);
         Assert.Equal(MessageBody.APPLICATION_JSON, msg.Body.ContentType);
         Assert.Equal(MessageBody.APPLICATION_JSON, msg.Header.ContentType);


    }
}
