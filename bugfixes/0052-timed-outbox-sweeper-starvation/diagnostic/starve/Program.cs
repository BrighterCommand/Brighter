using System.Collections.Concurrent;
using System.Diagnostics;
using Paramore.Brighter;
using Paramore.Brighter.Outbox.Hosting;

// args: mode(none|blocked|flood) seconds
var mode = args.Length > 0 ? args[0] : "none";
var seconds = args.Length > 1 ? int.Parse(args[1]) : 10;
var sw = Stopwatch.StartNew();
ThreadPool.GetMinThreads(out var minW, out _);
Console.WriteLine($"mode={mode} procs={Environment.ProcessorCount} minWorkers={minW}");

var timerTicks = new ConcurrentQueue<double>();
var threadTicks = new ConcurrentQueue<double>();
var asyncLoopTicks = new ConcurrentQueue<double>();

var stop = new CancellationTokenSource();
var gate = new ManualResetEventSlim(false);

// load
Thread? flooder = null;
if (mode == "blocked")
{
    for (int i = 0; i < 200; i++) Task.Run(() => gate.Wait());
}
else if (mode == "flood")
{
    // simulate Confluent's delivery thread: a non-pool thread doing Task.Run per delivery report
    flooder = new Thread(() =>
    {
        var perMs = 20; // 20k reports/sec
        while (!stop.IsCancellationRequested)
        {
            for (int i = 0; i < perMs; i++)
                Task.Run(() => { var s = Stopwatch.StartNew(); while (s.Elapsed.TotalMilliseconds < 0.2) { } });
            Thread.Sleep(1);
        }
    }) { IsBackground = true };
    flooder.Start();
}

// 1) the real TimedOutboxSweeper, ticks recorded by the lock
var sweeper = new TimedOutboxSweeper(null!, new RecordingLock(timerTicks, sw), new TimedOutboxSweeperOptions { TimerInterval = 1 });
await sweeper.StartAsync(CancellationToken.None);

// 2) control: dedicated thread
var t = new Thread(() => { while (!stop.IsCancellationRequested) { threadTicks.Enqueue(sw.Elapsed.TotalSeconds); Thread.Sleep(1000); } }) { IsBackground = true };
t.Start();

// 3) BackgroundService-style async loop with PeriodicTimer on a LongRunning task
_ = Task.Factory.StartNew(async () =>
{
    using var pt = new PeriodicTimer(TimeSpan.FromSeconds(1));
    asyncLoopTicks.Enqueue(sw.Elapsed.TotalSeconds);
    while (await pt.WaitForNextTickAsync()) asyncLoopTicks.Enqueue(sw.Elapsed.TotalSeconds);
}, TaskCreationOptions.LongRunning);

Thread.Sleep(seconds * 1000);
stop.Cancel();
gate.Set();
ThreadPool.GetAvailableThreads(out var avail, out _);
Console.WriteLine($"threadCount={ThreadPool.ThreadCount} pending={ThreadPool.PendingWorkItemCount}");
Report("TimedOutboxSweeper", timerTicks, seconds);
Report("dedicated thread", threadTicks, seconds);
Report("PeriodicTimer async loop", asyncLoopTicks, seconds);
Environment.Exit(0);

static void Report(string name, ConcurrentQueue<double> q, int seconds)
{
    var a = q.Where(x => x <= seconds).ToArray();
    var first = a.Length > 0 ? a[0] : double.NaN;
    var gaps = a.Zip(a.Skip(1), (x, y) => y - x).DefaultIfEmpty(double.NaN).Max();
    Console.WriteLine($"  {name,-38} ticks={a.Length,3} first={first,6:F2}s maxGap={gaps,6:F2}s");
}

class RecordingLock(ConcurrentQueue<double> q, Stopwatch sw) : IDistributedLock
{
    public Task<string?> ObtainLockAsync(string resource, CancellationToken ct) { q.Enqueue(sw.Elapsed.TotalSeconds); return Task.FromResult<string?>(null); }
    public Task ReleaseLockAsync(string resource, string lockId, CancellationToken ct) => Task.CompletedTask;
}
