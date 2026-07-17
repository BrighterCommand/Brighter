using System;
using System.IO.Compression;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Core.Tests.Compression;

public class CompressMessageUsingMemoryTests
{
    private readonly string _originalBody;
    private readonly RoutingKey _topic = new("test_topic");

    public CompressMessageUsingMemoryTests()
    {
        _originalBody = DataGenerator.CreateString(6000);
    }

    [Test]
    public async Task When_compressing_and_decompressing_should_round_trip_sync()
    {
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        var compressed = compressor.Wrap(message, new Publication { Topic = _topic });

        await Assert.That(compressed.Body.Memory.Length).IsLessThan(_originalBody.Length);

        var decompressor = new CompressPayloadTransformer();
        decompressor.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
        var decompressed = decompressor.Unwrap(compressed);

        await Assert.That(decompressed.Body.Value).IsEqualTo(_originalBody);
    }

    [Test]
    public async Task When_compressing_async_should_produce_compressed_output()
    {
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        var compressed = await compressor.WrapAsync(message, new Publication { Topic = _topic });

        var memory = compressed.Body.Memory;
        await Assert.That(memory.Length).IsLessThan(_originalBody.Length);
        await Assert.That(memory.Length).IsGreaterThanOrEqualTo(2);
        await Assert.That(memory.Span[0]).IsEqualTo((byte)0x1f);
        await Assert.That(memory.Span[1]).IsEqualTo((byte)0x8b);
    }

    [Test]
    public async Task When_checking_is_compressed_should_detect_via_memory_span()
    {
        var compressor = new CompressPayloadTransformer();
        compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 5);
        var message = CreateMessage(_originalBody);

        var compressed = compressor.Wrap(message, new Publication { Topic = _topic });

        var memory = compressed.Body.Memory;
        await Assert.That(memory.Length).IsGreaterThanOrEqualTo(2);
        await Assert.That(memory.Span[0]).IsEqualTo((byte)0x1f);
        await Assert.That(memory.Span[1]).IsEqualTo((byte)0x8b);
    }

    private Message CreateMessage(string body) =>
        new(
            new MessageHeader(Guid.NewGuid().ToString(), _topic, MessageType.MT_EVENT, timeStamp: DateTime.UtcNow),
            new MessageBody(body, new ContentType(MediaTypeNames.Application.Json), CharacterEncoding.UTF8));
}
