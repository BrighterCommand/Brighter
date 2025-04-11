using System;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class UncompressedPayloadTests
{
    
    [Fact]
    public void When_a_message_is_not_gzip_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        
        var smallContent = "small message";
        string mimeType = MessageBody.APPLICATION_JSON;

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );

        //act
        var msg = transformer.Unwrap(message);
        
        //assert
        Assert.Equal(smallContent, msg.Body.Value);
    }
    
    [Fact]
    public void When_a_message_is_not_zlib_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Zlib);
        
        var smallContent = "small message";
        string mimeType = MessageBody.APPLICATION_JSON;

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        //act
        var msg = transformer.Unwrap(message);
        
        //assert
        Assert.Equal(smallContent, msg.Body.Value);
    }
    
    [Fact]
    public void When_a_message_is_not_brotli_compressed()
    {
        
        //arrange
        var transformer = new CompressPayloadTransformer();
        transformer.InitializeUnwrapFromAttributeParams(CompressionMethod.Brotli);
        
        var smallContent = "small message";
        string mimeType = MessageBody.APPLICATION_JSON;

        var body = new MessageBody(smallContent, mimeType);
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new("test_topic"), MessageType.MT_EVENT, 
                timeStamp: DateTime.UtcNow, contentType: mimeType
            ),
            body
        );
        
        //act
        var msg = transformer.Unwrap(message);
        
        //assert
        Assert.Equal(smallContent, msg.Body.Value);
    }
}
