using System;

namespace Paramore.Brighter.Benchmarks;

public class BenchmarkCommand() : Command(Guid.NewGuid())
{
    public string Payload { get; set; } = string.Empty;
}
