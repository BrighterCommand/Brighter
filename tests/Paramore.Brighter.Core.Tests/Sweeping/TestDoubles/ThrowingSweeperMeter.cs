using System;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A sweeper meter whose every recording throws, as a broken metrics exporter might. The message
/// defaults to a fixed one; a test can pass its own to plant a unique marker.
/// </summary>
public sealed class ThrowingSweeperMeter(string message = "metrics are broken") : IAmABrighterSweeperMeter
{
    public void RecordTickLag(TimeSpan lag) => throw new InvalidOperationException(message);

    public void RecordSweep(SweepOutcome outcome, TimeSpan duration) => throw new InvalidOperationException(message);
}
