using Paramore.Brighter.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;

// args: sync|async workers seconds
var mode = args.Length > 0 ? args[0] : "async";
var workers = args.Length > 1 ? int.Parse(args[1]) : 64;
var seconds = args.Length > 2 ? int.Parse(args[2]) : 15;

var sw = Stopwatch.StartNew();
var counter = new Counter(sw);
ApplicationLogging.LoggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(counter));

var routingKey = new RoutingKey("MyCommand");
var producer = new FakeKafkaProducer(new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });
var mapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()), new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapper()));
mapperRegistry.Register<MyCommand, MyCommandMessageMapper>(); mapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapper>();
var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });
var resilience = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
var outbox = new InMemoryOutbox(TimeProvider.System) { EntryLimit = 1_000_000 };
var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
    producerRegistry, resilience, mapperRegistry,
    new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(),
    new BrighterTracer(TimeProvider.System), new FindPublicationByPublicationTopicOrRequestType(), outbox,
    maxOutStandingCheckInterval: Environment.GetEnvironmentVariable("NO_CHECK") == "1" ? TimeSpan.FromHours(1) : Environment.GetEnvironmentVariable("DI_DEFAULT") == "1" ? TimeSpan.Zero : null);
Console.WriteLine($"checkInterval={(Environment.GetEnvironmentVariable("DI_DEFAULT") == "1" ? "Zero (DI default)" : "1s (ctor default)")}");
var cp = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resilience, mediator,
    requestSchedulerFactory: new InMemorySchedulerFactory());

var services = new ServiceCollection();
services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
var sp = services.BuildServiceProvider();
var ticks = new ConcurrentQueue<double>();
var sweeper = new TimedOutboxSweeper(sp.GetRequiredService<IServiceScopeFactory>(), new RecordingLock(ticks, sw, new InMemoryLock()),
    new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.FromSeconds(1) });

ThreadPool.GetMinThreads(out var minW, out _);
Console.WriteLine($"mode={mode} workers={workers} procs={Environment.ProcessorCount} minWorkers={minW}");

await sweeper.StartAsync(CancellationToken.None);

var stop = new CancellationTokenSource();
long posts = 0; int reported = 0;
var loops = Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
{
    while (!stop.IsCancellationRequested)
    {
        try {
        if (mode == "sync") cp.Post(new MyCommand());
        else await cp.PostAsync(new MyCommand());
        } catch (Exception e) { if (Interlocked.Exchange(ref reported, 1) == 0) Console.WriteLine("POST FAILED: " + e); throw; }
        Interlocked.Increment(ref posts);
        await Task.Yield();
    }
})).ToArray();

// sampler on dedicated thread
var samples = new List<string>();
var sampler = new Thread(() =>
{
    for (int s = 1; s <= seconds; s++)
    {
        Thread.Sleep(1000);
        var outstanding = outbox.Requests_Outstanding();
        samples.Add($"t={s,2}s posts={Interlocked.Read(ref posts),8} undispatched={outstanding,7} entries={outbox.EntryCount,8} pool={ThreadPool.ThreadCount,3} queue={ThreadPool.PendingWorkItemCount,8} checksQueued={counter.Queued,6} checksStarted={counter.Started,6} backlog={counter.Queued - counter.Started,6} sweepTicks={ticks.Count,3} sweeps={counter.Sweeps,3} resends={producer.Resends,6}");
    }
}) { IsBackground = true };
sampler.Start();
sampler.Join();
stop.Cancel();
foreach (var l in samples) Console.WriteLine(l);
var a = ticks.ToArray();
var gaps = a.Zip(a.Skip(1), (x, y) => y - x).DefaultIfEmpty(double.NaN).Max();
Console.WriteLine($"sweeper ticks={a.Length} maxGap={gaps:F2}s  sweepsCompleted={counter.Sweeps} maxSweep={counter.MaxSweep:F2}s maxCheckBacklog={counter.MaxBacklog}");
Environment.Exit(0);

static class OutboxExt
{
    public static int Requests_Outstanding(this InMemoryOutbox o) =>
        o.GetOutstandingMessageCount(TimeSpan.Zero, null);
}

class Counter(Stopwatch sw) : ILoggerProvider, ILogger
{
    public long Queued, Started, Sweeps; public long MaxBacklog; public double MaxSweep;
    double _sweepStart;
    public ILogger CreateLogger(string categoryName) => this;
    public void Dispose() { }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fmt = (state as IReadOnlyList<KeyValuePair<string, object?>>)?.LastOrDefault(kv => kv.Key == "{OriginalFormat}").Value as string ?? "";
        if (fmt.StartsWith("Running outstanding message check")) { var q = Interlocked.Increment(ref Queued); var b = q - Interlocked.Read(ref Started); if (b > MaxBacklog) MaxBacklog = b; }
        else if (fmt.StartsWith("Begin count of outstanding")) Interlocked.Increment(ref Started);
        else if (fmt.StartsWith("Outbox Sweeper looking")) _sweepStart = sw.Elapsed.TotalSeconds;
        else if (fmt.StartsWith("Outbox Sweeper sleeping") && _sweepStart > 0) { Interlocked.Increment(ref Sweeps); var d = sw.Elapsed.TotalSeconds - _sweepStart; if (d > MaxSweep) MaxSweep = d; _sweepStart = 0; }
        else if (logLevel >= LogLevel.Warning && !fmt.StartsWith("Outbox Sweeper is still running")) Console.WriteLine($"[{logLevel}] {formatter(state, exception)} {exception?.GetType().Name}");
    }
}

class RecordingLock(ConcurrentQueue<double> q, Stopwatch sw, IDistributedLock inner) : IDistributedLock
{
    public Task<string?> ObtainLockAsync(string r, CancellationToken ct) { q.Enqueue(sw.Elapsed.TotalSeconds); return inner.ObtainLockAsync(r, ct); }
    public Task ReleaseLockAsync(string r, string id, CancellationToken ct) => inner.ReleaseLockAsync(r, id, ct);
}

// Mimics KafkaMessageProducer: Produce enqueues to librdkafka; delivery reports arrive on a non-pool
// thread; Brighter raises each confirmation with Task.Run (KafkaMessageProducer.cs:456).
class FakeKafkaProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation, ISupportPublishConfirmationAsync
{
    private readonly BlockingCollection<Id> _inFlight = new();
    private readonly ConcurrentDictionary<string, byte> _seen = new();
    public long Resends;
    private Func<PublishConfirmationResult, Task>? _async;
    public FakeKafkaProducer(Publication p)
    {
        Publication = p;
        new Thread(() =>
        {
            foreach (var id in _inFlight.GetConsumingEnumerable())
            {
                var r = new PublishConfirmationResult(true, id, Publication.Topic!, null);
                Task.Run(async () => { OnMessagePublished?.Invoke(r); if (_async != null) await _async(r); });
            }
        }) { IsBackground = true, Name = "librdkafka-delivery" }.Start();
    }
    public bool UseAsyncPublishConfirmation => true;
    public Publication Publication { get; }
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }
    public event Action<PublishConfirmationResult>? OnMessagePublished;
    public event Func<PublishConfirmationResult, Task> OnMessagePublishedAsync { add => _async += value; remove => _async -= value; }
    public void Send(Message m) { if (!_seen.TryAdd(m.Id.Value, 0)) Interlocked.Increment(ref Resends); _inFlight.Add(m.Id); }
    public void SendWithDelay(Message m, TimeSpan? d) => Send(m);
    public Task SendAsync(Message m, CancellationToken ct = default) { Send(m); return Task.CompletedTask; }
    public Task SendWithDelayAsync(Message m, TimeSpan? d, CancellationToken ct = default) => SendAsync(m, ct);
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class MyCommand() : Command(Id.Random()) { public string? Value { get; set; } = "hello"; }

class MyCommandMessageMapper : IAmAMessageMapper<MyCommand>, IAmAMessageMapperAsync<MyCommand>
{
    public IRequestContext? Context { get; set; }
    public Message MapToMessage(MyCommand request, Publication publication) =>
        new(new MessageHeader(request.Id, publication.Topic!, MessageType.MT_COMMAND, timeStamp: DateTimeOffset.UtcNow,
                contentType: new ContentType(MediaTypeNames.Application.Json)),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)));
    public MyCommand MapToRequest(Message message) => throw new NotImplementedException();
    public Task<Message> MapToMessageAsync(MyCommand r, Publication p, CancellationToken ct = default) => Task.FromResult(MapToMessage(r, p));
    public Task<MyCommand> MapToRequestAsync(Message m, CancellationToken ct = default) => throw new NotImplementedException();
}
