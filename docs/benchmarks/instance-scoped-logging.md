# Instance-scoped logging benchmark

This benchmark compares the instance-scoped implementation before logger reuse with the same decorated send after factory-scoped caching and lifetime-logger reuse. It does not compare against the old process-wide static factory.

Each operation sends one command through one logging decorator. Logging uses the default minimum level with no output providers, and tracing is disabled, so the measurement covers pipeline construction and logger plumbing rather than console or exporter I/O. Setup creates the container and warms the pipeline outside the measured operation. Both measurements use BenchmarkDotNet 0.15.8 DefaultJob with MemoryDiagnoser, .NET SDK 10.0.401, .NET 10.0.12, and an Intel Core i7-12700K on Windows 11. Builds and tests are stopped during measurement.

## Results

| Implementation | Mean | Error | StdDev | Allocated per send |
| --- | ---: | ---: | ---: | ---: |
| Before logger reuse | 2.399 us | 0.0482 us | 0.1353 us | 8.67 KB |
| With logger reuse | 1.862 us | 0.0462 us | 0.1296 us | 7.45 KB |

The measured mean is 22.4% lower and allocation is 14.1% lower in this scenario. Error and StdDev are the values reported by BenchmarkDotNet; these results are specific to this machine and workload.

This is a single-threaded microbenchmark. It does not measure transport throughput, logger-provider I/O, or contention between concurrent pumps.

## Reproduction

Create a console project named `LoggingBench` outside the repository, targeting `net10.0`. Add BenchmarkDotNet 0.15.8 and a project reference to `src/Paramore.Brighter.Extensions.DependencyInjection/Paramore.Brighter.Extensions.DependencyInjection.csproj`. Keep the assembly name `LoggingBench`: Brighter automatically scans assemblies whose names start with `Paramore.Brighter`, while this harness explicitly registers its handler.

Use this `Program.cs`:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Observability;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
public class LoggingPipelineBenchmark
{
    private ServiceProvider _provider = null!;
    private IAmACommandProcessor _processor = null!;
    private readonly ProbeCommand _command = new();

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBrighter(options => options.InstrumentationOptions = InstrumentationOptions.None)
            .Handlers(registry => registry.Register<ProbeCommand, ProbeHandler>());
        _provider = services.BuildServiceProvider();
        _processor = _provider.GetRequiredService<IAmACommandProcessor>();
        _processor.Send(_command);
    }

    [Benchmark]
    public int SendDecorated()
    {
        _processor.Send(_command);
        return _command.Count;
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();
}

public sealed class ProbeCommand() : Command(Id.Random())
{
    public int Count { get; set; }
}

public sealed class ProbeHandler : RequestHandler<ProbeCommand>
{
    [RequestLogging(1, HandlerTiming.Before)]

    public override ProbeCommand Handle(ProbeCommand command)
    {
        command.Count++;
        return base.Handle(command);
    }
}

```

Build in Release, validate once with the Dry job, then measure with the default job:

```shell
dotnet run -c Release -- --filter '*SendDecorated*' --job Dry --noOverwrite
dotnet run -c Release --no-build -- --filter '*SendDecorated*' --noOverwrite
```

For a before/after comparison, keep this harness unchanged and run it against each implementation. The regression test `PipelineLoggingReuseTests` separately asserts that warmed synchronous and asynchronous pipelines do not call `ILoggerFactory.CreateLogger` again. Temporarily bypassing the cache makes that assertion fail: factory calls rise from 15 to 25 after the second pair of sends. The mutation is removed from the implementation.
