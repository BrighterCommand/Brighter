using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressMessageUsingMemoryTests
{
    private readonly string _originalBody;
    private readonly RoutingKey _topic = new("test_topic");

    public CompressMessageUsingMemoryTests()
    {
        _originalBody = DataGenerator.CreateString(6000);
    }

    [Fact]
    public void When_compressing_and_decompressing_should_round_trip_sync()
    {
        // Arrange
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        // Act — compress
        var compressed = compressor.Wrap(message, new Publication { Topic = _topic });

        // Assert — compressed body is smaller
        Assert.True(compressed.Body.Memory.Length < _originalBody.Length);

        // Act — decompress
        var decompressor = new CompressPayloadTransformer();
        decompressor.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        var decompressed = decompressor.Unwrap(compressed);

        // Assert — round-trip recovers original
        Assert.Equal(_originalBody, decompressed.Body.Value);
    }

    [Fact]
    public async Task When_compressing_async_should_produce_compressed_output()
    {
        // Arrange
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        // Act
        var compressed = await compressor.WrapAsync(message, new Publication { Topic = _topic });

        // Assert — compressed body is smaller and starts with gzip magic bytes
        var span = compressed.Body.Memory.Span;
        Assert.True(span.Length < _originalBody.Length);
        Assert.True(span.Length >= 2);
        Assert.Equal(0x1f, span[0]);
        Assert.Equal(0x8b, span[1]);
    }

    [Fact]
    public void When_checking_is_compressed_should_detect_via_memory_span()
    {
        // Arrange
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        // Act
        var compressed = compressor.Wrap(message, new Publication { Topic = _topic });

        // Assert — gzip magic bytes are accessible via Memory.Span
        var span = compressed.Body.Memory.Span;
        Assert.True(span.Length >= 2);
        Assert.Equal(0x1f, span[0]);
        Assert.Equal(0x8b, span[1]);
    }

    private Message CreateMessage(string body) =>
        new(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
}
