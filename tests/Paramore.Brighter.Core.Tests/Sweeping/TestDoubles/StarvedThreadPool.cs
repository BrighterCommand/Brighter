using System;
using System.Threading;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// Caps the thread pool at its minimum size and parks every worker on a gate, so that no pool thread
/// is free to run anything — not even a <see cref="Timer"/> callback — until <see cref="Dispose"/>.
/// Process-wide: only use from a test in <see cref="ThreadPoolStarvationCollection"/>.
/// </summary>
public sealed class StarvedThreadPool : IDisposable
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan s_probeWindow = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan s_drainTimeout = TimeSpan.FromSeconds(30);

    private readonly ManualResetEventSlim _gate = new(false);
    private readonly CountdownEvent _blockersFinished = new(1);
    private readonly int _maxWorkers;
    private readonly int _maxCompletionPorts;

    public StarvedThreadPool()
    {
        ThreadPool.GetMaxThreads(out _maxWorkers, out _maxCompletionPorts);
        try
        {
            Assert.True(ThreadPool.SetMaxThreads(Environment.ProcessorCount, _maxCompletionPorts), "Could not cap the thread pool");
            Assert.True(BlockEveryWorker(), "Could not block every thread-pool worker");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Opens the gate, restores the pool's limits and waits for every blocked item to finish.</summary>
    public void Dispose()
    {
        _gate.Set();
        ThreadPool.SetMaxThreads(_maxWorkers, _maxCompletionPorts);
        _blockersFinished.Signal();
        _blockersFinished.Wait(s_drainTimeout);
        _blockersFinished.Dispose();
        _gate.Dispose();
    }

    // Threads left over from earlier tests can sit above the cap until they go idle, so keep queueing
    // blockers until a probe timer — the same mechanism the sweeper uses — can no longer fire.
    private bool BlockEveryWorker()
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            QueueBlockers(Math.Max(ThreadPool.ThreadCount, Environment.ProcessorCount) * 2);
            if (!TimerCanFire()) return true;
        }
        return false;
    }

    private void QueueBlockers(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _blockersFinished.AddCount();
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                _gate.Wait();
                _blockersFinished.Signal();
            }, null);
        }
    }

    private static bool TimerCanFire()
    {
        using var fired = new ManualResetEventSlim(false);
        using var probe = new Timer(_ => SetIfNotDisposed(fired), null, TimeSpan.Zero, System.Threading.Timeout.InfiniteTimeSpan);
        var result = fired.Wait(s_probeWindow);
        probe.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
        return result;
    }

    private static void SetIfNotDisposed(ManualResetEventSlim fired)
    {
        try { fired.Set(); }
        catch (ObjectDisposedException) { }
    }
}
