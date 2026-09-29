using System;
using System.IO;


namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// The ledger is the gate: it decides which canonical facts carry a Deferred Skip. When the
/// generator cannot find it, the only safe answer is to stop.
/// </summary>
/// <remarks>
/// Generating anyway fails silently rather than loudly. With no ledger the generator installs no
/// prepare-model hook, so <see cref="Configuration.MessagingGatewayConfiguration.Skip"/> is never
/// assigned and stays null - and under Liquid <c>nil != empty</c> is TRUE, so
/// <c>{% if Skip != empty %}</c> renders. Every canonical fact across every transport comes out as
/// <c>[Test][Skip("")]</c>, which xUnit reports as skipped. The suite would go green having run
/// nothing. That is the failure mode this test exists to prevent, and it is not hypothetical: the
/// search walks up from <see cref="AppContext.BaseDirectory"/> for
/// <c>specs/0036-.../conformance-status.md</c>, which misses whenever the generator runs outside
/// the repository tree, and will miss for everyone once this spec directory is archived.
/// </remarks>
public class LedgerResolutionFailureTests : IDisposable
{
    private readonly string _testDirectory;

    public LedgerResolutionFailureTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LedgerResolutionTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Test]
    public async System.Threading.Tasks.Task When_no_ledger_exists_above_the_start_directory_should_throw()
    {
        // Arrange - a directory with no ledger anywhere above it, which is what the generator sees
        // when it runs outside the repository, or once this spec directory is archived.
        var directoryWithNoLedger = Path.Combine(_testDirectory, "no-ledger-above-here");
        Directory.CreateDirectory(directoryWithNoLedger);

        // Act / Assert - loud, naming what was looked for so the cause is obvious from the message.
        var exception = await Assert.That(() => ConformanceLedger.LoadFrom(directoryWithNoLedger)).ThrowsExactly<InvalidOperationException>();

        await Assert.That(exception.Message).Contains("conformance-status.md");
    }

    [Test]
    public async System.Threading.Tasks.Task When_a_ledger_exists_above_the_start_directory_should_load_it()
    {
        // Arrange - the repository's own tree, where the real ledger is findable. Guards against
        // "make it throw" being satisfied by throwing unconditionally.
        var directoryInsideTheRepository = AppContext.BaseDirectory;

        // Act
        var ledger = ConformanceLedger.LoadFrom(directoryInsideTheRepository);

        // Assert - a usable ledger, not a stub: it resolves a row the conformance matrix carries.
        await Assert.That(ledger.HasRow("Redis / RedisMessagingGateway")).IsTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
