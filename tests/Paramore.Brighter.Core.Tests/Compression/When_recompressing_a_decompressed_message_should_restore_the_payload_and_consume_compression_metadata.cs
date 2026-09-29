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

namespace Paramore.Brighter.Core.Tests.Compression;

public class DecompressedMessageRecompressionTests
{
    [Test]
    [Arguments(CompressionMethod.GZip, false, false)]
    [Arguments(CompressionMethod.GZip, true, true)]
    [Arguments(CompressionMethod.GZip, false, true)]
    [Arguments(CompressionMethod.GZip, true, false)]
    [Arguments(CompressionMethod.Zlib, false, false)]
    [Arguments(CompressionMethod.Zlib, true, true)]
    [Arguments(CompressionMethod.Zlib, false, true)]
    [Arguments(CompressionMethod.Zlib, true, false)]
    [Arguments(CompressionMethod.Brotli, false, false)]
    [Arguments(CompressionMethod.Brotli, true, true)]
    [Arguments(CompressionMethod.Brotli, false, true)]
    [Arguments(CompressionMethod.Brotli, true, false)]
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
        await Assert.That(recompressedBody.CharacterEncoding).IsEqualTo(CharacterEncoding.Raw);
        await Assert.That(recompressedBody.Memory.Length < originalBytes.Length).IsTrue();
        await Assert.That(savedContentType).IsEqualTo(originalContentType.ToString());
        await Assert.That(restored.Body.Memory.ToArray()).IsEqualTo(originalBytes);
        await Assert.That(restored.Body.Value).IsEqualTo(payload);
        await Assert.That(restored.Header.ContentType).IsEqualTo(originalContentType);
        await Assert.That(restored.Body.ContentType).IsEqualTo(originalContentType);
        await Assert.That(restored.Header.Bag.ContainsKey(CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER)).IsFalse();
        await Assert.That(restored.Header.Bag["custom-header"]).IsEqualTo("preserved");
        await Assert.That(restored.Header.Bag).HasSingleItem();
    }
}
