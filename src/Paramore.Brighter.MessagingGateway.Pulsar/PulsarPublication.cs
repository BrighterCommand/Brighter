using System;
using System.Buffers;
using System.Diagnostics;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarPublication : Publication
{
    public CompressionType CompressionType { get; set; } = CompressionType.None;
    
    public string? Name { get; set; }
    public byte[]? SchemaVersion { get; set; }
    public ulong InitialSequenceId { get; set; }
    public ProducerAccessMode AccessMode { get; set; } = ProducerAccessMode.Shared;

    public ISchema<ReadOnlySequence<byte>> Schema { get; set; } = DotPulsar.Schema.ByteSequence; 

    public Func<Message, ulong> GenerateSequenceId { get; set; } = _ => 0;
    
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
    
    public InstrumentationOptions? Instrumentation { get; set; }
    
    public Action<IProducerBuilder<ReadOnlySequence<byte>>>? Configure { get; set; }
}

public class PulsarPublication<T> : PulsarPublication
    where T : IRequest
{
    public PulsarPublication()
    {
        RequestType = typeof(T);
    }
}
