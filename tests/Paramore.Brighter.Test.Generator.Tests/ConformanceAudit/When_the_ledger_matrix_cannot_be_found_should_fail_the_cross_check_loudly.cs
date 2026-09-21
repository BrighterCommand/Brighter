using System;
using System.IO;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// The cross-check reads the ledger through <see cref="ConformanceLedger"/>, the generator's own
/// parser, so that the audit cannot disagree with the generator about what the ledger says.
///
/// That shared parser refuses to return an empty matrix, and the audit inherits the refusal rather
/// than softening it. An unreadable ledger is the one case where silence is the most misleading
/// answer available: every cell would resolve to "no Skip", so every deferred behaviour would be
/// reported as running, and a cross-check reporting zero violations would agree.
/// </summary>
public class LedgerCrossCheckUnreadableLedgerTests
{
    [Fact]
    public void When_the_ledger_matrix_cannot_be_found_should_fail_the_cross_check_loudly()
    {
        // Arrange — a ledger with prose but no matrix header at all
        var repoRoot = BuildRepoWithLedger("# Conformance status\n\nNo matrix here.\n");

        try
        {
            // Act / Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, LedgerPath(repoRoot)));

            Assert.Contains("Could not find the conformance matrix", exception.Message);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_the_ledger_matrix_has_no_data_rows_should_fail_the_cross_check_loudly()
    {
        // Arrange — the header is present and well formed, but nothing follows it
        var repoRoot = BuildRepoWithLedger(
            "# Conformance status\n\n| Configuration | FR-2 | FR-4 |\n|---|---|---|\n\nProse.\n");

        try
        {
            // Act / Assert
            var exception = Assert.Throws<InvalidOperationException>(
                () => LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, LedgerPath(repoRoot)));

            Assert.Contains("holds no data rows", exception.Message);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildRepoWithLedger(string ledgerContent)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"ledger-unreadable-canary-{Guid.NewGuid():N}");

        Directory.CreateDirectory(Path.GetDirectoryName(LedgerPath(repoRoot))!);
        File.WriteAllText(LedgerPath(repoRoot), ledgerContent);

        // The tree side of the cross-check is irrelevant here, but the roots must exist so that a
        // missing directory is never what makes the test pass.
        Directory.CreateDirectory(Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Canary.Tests",
            "MessagingGateway", "Generated", "Reactor"));
        Directory.CreateDirectory(Path.Combine(
            repoRoot, "tools", "Paramore.Brighter.Test.Generator",
            "Templates", "MessagingGateway"));

        return repoRoot;
    }

    private static string LedgerPath(string repoRoot) => Path.Combine(
        repoRoot, "specs", "0036-universal-transport-conformance-tests", "conformance-status.md");
}
