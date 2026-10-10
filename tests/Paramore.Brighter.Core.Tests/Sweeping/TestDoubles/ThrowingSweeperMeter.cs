using System;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A sweeper meter whose every recording throws, as a broken metrics exporter might.
/// </summary>
public sealed class ThrowingSweeperMeter : IAmABrighterSweeperMeter
{
    public void RecordTickLag(TimeSpan lag) => throw new InvalidOperationException("metrics are broken");

    public void RecordSweep(SweepOutcome outcome, TimeSpan duration) => throw new InvalidOperationException("metrics are broken");
}
