#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.IO.Compression;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class AsyncCompressedMessageRoundTripTests
{
    [Theory]
    [InlineData(CompressionMethod.GZip, false)]
    [InlineData(CompressionMethod.GZip, true)]
    [InlineData(CompressionMethod.Zlib, false)]
    [InlineData(CompressionMethod.Zlib, true)]
    [InlineData(CompressionMethod.Brotli, false)]
    [InlineData(CompressionMethod.Brotli, true)]
    public async Task When_decompressing_an_async_compressed_message_should_restore_the_original_payload(
        CompressionMethod compressionMethod, bool decompressAsync)
    {
        //Arrange
        var payload = "{\"greeting\":\"" + new string('h', 4096) + "\"}";
        var originalBytes = Encoding.UTF8.GetBytes(payload);
        var originalContentType = new ContentType("application/json; charset=utf-8");
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("compression-round-trip"), MessageType.MT_EVENT,
                contentType: new ContentType(originalContentType.ToString())),
            new MessageBody(payload, new ContentType(originalContentType.ToString())));

        using var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(compressionMethod, CompressionLevel.Optimal, 0);
        var compressed = await compressor.WrapAsync(message, new Publication());
        Assert.Equal("utf-8", compressed.Header.ContentType.CharSet);
        Assert.True(compressed.Body.Memory.Length < originalBytes.Length);

        using var decompressor = new CompressPayloadTransformer();
        decompressor.InitializeUnwrapFromAttributeParams(compressionMethod);

        //Act
        var decompressed = decompressAsync
            ? await decompressor.UnwrapAsync(compressed)
            : decompressor.Unwrap(compressed);

        //Assert
        Assert.Equal(originalBytes, decompressed.Body.Bytes);
        Assert.Equal(payload, decompressed.Body.Value);
        Assert.Equal(originalContentType, decompressed.Header.ContentType);
        Assert.Equal(originalContentType, decompressed.Body.ContentType);
    }
}
