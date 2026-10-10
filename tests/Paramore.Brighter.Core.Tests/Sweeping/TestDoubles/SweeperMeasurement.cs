namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>One value recorded on a sweeper instrument, with its outcome attribute if it had one.</summary>
public sealed record SweeperMeasurement(string Instrument, double Value, string? Outcome);
