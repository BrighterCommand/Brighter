#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia

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

public class DecompressedMessageRecompressionTests
{
    [Theory]
    [InlineData(CompressionMethod.GZip, false, false)]
    [InlineData(CompressionMethod.GZip, true, true)]
    [InlineData(CompressionMethod.GZip, false, true)]
    [InlineData(CompressionMethod.GZip, true, false)]
    [InlineData(CompressionMethod.Zlib, false, false)]
    [InlineData(CompressionMethod.Zlib, true, true)]
    [InlineData(CompressionMethod.Zlib, false, true)]
    [InlineData(CompressionMethod.Zlib, true, false)]
    [InlineData(CompressionMethod.Brotli, false, false)]
    [InlineData(CompressionMethod.Brotli, true, true)]
    [InlineData(CompressionMethod.Brotli, false, true)]
    [InlineData(CompressionMethod.Brotli, true, false)]
    public async Task When_recompressing_a_decompressed_message_should_restore_the_payload_and_consume_compression_metadata(
        CompressionMethod compressionMethod, bool compressAsync, bool decompressAsync)
    {
        //Arrange
        var payload = "{\"greeting\":\"" + new string('h', 4096) + " café\"}";
        var originalBytes = Encoding.UTF8.GetBytes(payload);
        var originalContentType = new ContentType("application/json; charset=utf-8");
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("compression-round-trip"), MessageType.MT_EVENT,
                contentType: new ContentType(originalContentType.ToString())),
            new MessageBody(payload, new ContentType(originalContentType.ToString())));
        message.Header.Bag.Add("custom-header", "preserved");

        using var transformer = new CompressPayloadTransformer();
        transformer.InitializeWrapFromAttributeParams(compressionMethod, CompressionLevel.Optimal, 1);
        transformer.InitializeUnwrapFromAttributeParams(compressionMethod);
        var publication = new Publication();
        var compressed = compressAsync
            ? await transformer.WrapAsync(message, publication)
            : transformer.Wrap(message, publication);

        //Act
        var decompressed = decompressAsync
            ? await transformer.UnwrapAsync(compressed)
            : transformer.Unwrap(compressed);
        var recompressed = compressAsync
            ? await transformer.WrapAsync(decompressed, publication)
            : transformer.Wrap(decompressed, publication);
        var recompressedBody = recompressed.Body;
        var savedContentType = recompressed.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER];
        var restored = decompressAsync
            ? await transformer.UnwrapAsync(recompressed)
            : transformer.Unwrap(recompressed);

        //Assert
        Assert.Equal(CharacterEncoding.Raw, recompressedBody.CharacterEncoding);
        Assert.True(recompressedBody.Memory.Length < originalBytes.Length);
        Assert.Equal(originalContentType.ToString(), savedContentType);
        Assert.Equal(originalBytes, restored.Body.Memory.ToArray());
        Assert.Equal(payload, restored.Body.Value);
        Assert.Equal(originalContentType, restored.Header.ContentType);
        Assert.Equal(originalContentType, restored.Body.ContentType);
        Assert.False(restored.Header.Bag.ContainsKey(CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER));
        Assert.Equal("preserved", restored.Header.Bag["custom-header"]);
        Assert.Single(restored.Header.Bag);
    }
}
