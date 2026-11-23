using System;
using System.IO;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// The ledger is the gate: it decides which canonical facts carry a Deferred Skip. When the
/// generator cannot find it, the only safe answer is to stop.
/// </summary>
/// <remarks>
/// Generating anyway fails silently: every canonical fact renders as skipped and the suite goes green having
/// run nothing (see <see cref="ConformanceLedger.LoadFrom"/>). The search misses whenever the generator runs
/// outside the repository tree. A fact against the repository's own tree guards against throwing unconditionally.
/// </remarks>
public class LedgerResolutionFailureTests : IDisposable
{
    private readonly string _testDirectory;

    public LedgerResolutionFailureTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LedgerResolutionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void When_no_ledger_exists_above_the_start_directory_should_throw()
    {
        // Arrange
        var directoryWithNoLedger = Path.Combine(_testDirectory, "no-ledger-above-here");
        Directory.CreateDirectory(directoryWithNoLedger);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConformanceLedger.LoadFrom(directoryWithNoLedger));

        Assert.Contains("conformance-status.md", exception.Message);
    }

    [Fact]
    public void When_a_ledger_exists_above_the_start_directory_should_load_it()
    {
        // Arrange
        var directoryInsideTheRepository = AppContext.BaseDirectory;

        // Act
        var ledger = ConformanceLedger.LoadFrom(directoryInsideTheRepository);

        // Assert
        Assert.True(ledger.HasRow("Redis / RedisMessagingGateway"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
