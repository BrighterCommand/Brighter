using System;
using System.Threading;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A sweeper meter that reads the sweeper thread's allocated bytes when a sweep's tick lag is recorded (the
/// start of the measured window) and when the sweep is recorded (its end). It stores the readings in arrays
/// allocated up front, so the double itself allocates nothing inside the window.
/// </summary>
public sealed class AllocationReadingSweeperMeter : IAmABrighterSweeperMeter
{
    private readonly long[] _atTickLag;
    private readonly long[] _atSweep;
    private int _sweeps;

    /// <param name="capacity">The most sweeps that can be recorded; later ones are ignored.</param>
    public AllocationReadingSweeperMeter(int capacity)
    {
        _atTickLag = new long[capacity];
        _atSweep = new long[capacity];
    }

    /// <summary>The number of sweeps whose allocation has been recorded.</summary>
    public int Sweeps => Volatile.Read(ref _sweeps);

    /// <summary>The bytes the sweeper thread allocated between recording tick lag and recording the sweep, for sweep <paramref name="index"/> (zero-based).</summary>
    public long BytesAllocatedBySweep(int index) => _atSweep[index] - _atTickLag[index];

    public void RecordTickLag(TimeSpan lag)
    {
        var index = _sweeps;
        if (index < _atTickLag.Length)
            _atTickLag[index] = GC.GetAllocatedBytesForCurrentThread();
    }

    public void RecordSweep(SweepOutcome outcome, TimeSpan duration)
    {
        var index = _sweeps;
        if (index >= _atSweep.Length) return;

        _atSweep[index] = GC.GetAllocatedBytesForCurrentThread();
        Volatile.Write(ref _sweeps, index + 1);
    }
}
