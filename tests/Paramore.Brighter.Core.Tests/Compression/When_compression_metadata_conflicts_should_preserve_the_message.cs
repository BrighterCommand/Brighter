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

using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressionMetadataConflictTests
{
    [Test]
    [Arguments(CompressionMethod.GZip, false)]
    [Arguments(CompressionMethod.GZip, true)]
    [Arguments(CompressionMethod.Zlib, false)]
    [Arguments(CompressionMethod.Zlib, true)]
    [Arguments(CompressionMethod.Brotli, false)]
    [Arguments(CompressionMethod.Brotli, true)]
    public async Task When_compression_metadata_conflicts_should_preserve_the_message(
        CompressionMethod compressionMethod, bool useAsync)
    {
        //Arrange
        var payload = new string('h', 4096);
        var originalContentType = new ContentType("text/plain; charset=utf-8");
        var originalBody = new MessageBody(payload, new ContentType(originalContentType.ToString()));
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("compression-failure"), MessageType.MT_EVENT,
                contentType: new ContentType(originalContentType.ToString())),
            originalBody);
        message.Header.Bag.Add(CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER, "application/xml");
        message.Header.Bag.Add("custom-header", "preserved");

        using var transformer = new CompressPayloadTransformer();
        transformer.InitializeWrapFromAttributeParams(compressionMethod, CompressionLevel.Optimal, 1);
        var publication = new Publication();

        //Act
        if (useAsync)
        {
            await Assert.That(() => transformer.WrapAsync(message, publication)).ThrowsExactly<ArgumentException>();
        }
        else
        {
            await Assert.That(() => transformer.Wrap(message, publication)).ThrowsExactly<ArgumentException>();
        }

        //Assert
        await Assert.That(message.Body).IsSameReferenceAs(originalBody);
        await Assert.That(message.Body.Value).IsEqualTo(payload);
        await Assert.That(message.Header.ContentType).IsEqualTo(originalContentType);
        await Assert.That(message.Body.ContentType).IsEqualTo(originalContentType);
        await Assert.That(message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]).IsEqualTo("application/xml");
        await Assert.That(message.Header.Bag["custom-header"]).IsEqualTo("preserved");
        await Assert.That(message.Header.Bag.Count).IsEqualTo(2);
    }
}
