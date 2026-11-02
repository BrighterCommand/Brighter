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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressionMetadataConflictTests
{
    [Theory]
    [InlineData(CompressionMethod.GZip, false)]
    [InlineData(CompressionMethod.GZip, true)]
    [InlineData(CompressionMethod.Zlib, false)]
    [InlineData(CompressionMethod.Zlib, true)]
    [InlineData(CompressionMethod.Brotli, false)]
    [InlineData(CompressionMethod.Brotli, true)]
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
            await Assert.ThrowsAsync<ArgumentException>(() => transformer.WrapAsync(message, publication));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => transformer.Wrap(message, publication));
        }

        //Assert
        Assert.Same(originalBody, message.Body);
        Assert.Equal(payload, message.Body.Value);
        Assert.Equal(originalContentType, message.Header.ContentType);
        Assert.Equal(originalContentType, message.Body.ContentType);
        Assert.Equal("application/xml", message.Header.Bag[CompressPayloadTransformer.ORIGINAL_CONTENTTYPE_HEADER]);
        Assert.Equal("preserved", message.Header.Bag["custom-header"]);
        Assert.Equal(2, message.Header.Bag.Count);
    }
}
