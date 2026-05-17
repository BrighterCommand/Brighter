using System;
using System.IO.Compression;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.Transforms.Transformers;

namespace Paramore.Brighter.Benchmarks;

[MemoryDiagnoser]
public class MessageRoundTripBenchmark
{
    private BenchmarkCommand _command = null!;
    private Publication _publication = null!;
    private JsonMessageMapper<BenchmarkCommand> _mapper = null!;
    private CompressPayloadTransformer _compressor = null!;
    private CompressPayloadTransformer _decompressor = null!;

    [Params(1_000, 10_000, 100_000)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _command = new BenchmarkCommand
        {
            Payload = new string('A', PayloadSize)
        };

        _publication = new Publication
        {
            Topic = new RoutingKey("benchmark.topic"),
            Source = new Uri("http://benchmark"),
            Type = new CloudEventsType("benchmark.command")
        };

        _mapper = new JsonMessageMapper<BenchmarkCommand>
        {
            Context = new RequestContext()
        };

        _compressor = new CompressPayloadTransformer();
        _compressor.InitializeWrapFromAttributeParams(CompressionMethod.GZip, CompressionLevel.Optimal, 0);

        _decompressor = new CompressPayloadTransformer();
        _decompressor.InitializeUnwrapFromAttributeParams(CompressionMethod.GZip);
    }

    [Benchmark]
    public Message MapToMessage()
    {
        return _mapper.MapToMessage(_command, _publication);
    }

    [Benchmark]
    public Message MapToMessage_ThenCompress()
    {
        var message = _mapper.MapToMessage(_command, _publication);
        return _compressor.Wrap(message, _publication);
    }

    [Benchmark]
    public BenchmarkCommand FullRoundTrip()
    {
        var message = _mapper.MapToMessage(_command, _publication);
        var compressed = _compressor.Wrap(message, _publication);
        var decompressed = _decompressor.Unwrap(compressed);
        return _mapper.MapToRequest(decompressed);
    }
}
