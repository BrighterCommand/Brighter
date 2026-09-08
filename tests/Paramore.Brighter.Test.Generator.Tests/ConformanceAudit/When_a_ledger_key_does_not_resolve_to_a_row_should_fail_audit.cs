using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// The ledger lookup fails open: <c>ConformanceLedger.GetSkip</c> returns the empty string —
/// meaning <em>run, no Skip</em> — when the row key is absent, when the FR column is absent, and
/// when the cell value matches no known vocabulary token. The cell-agreement audit derives its
/// expectation from that same call, so both sides compute the empty string, agree with each other,
/// and the drift is invisible.
///
/// The PR's stated invariant is <em>no silent skips</em>. This is its mirror — <em>no silent
/// un-skips</em> — and it is the direction that a misspelled <c>LedgerKey</c>, a renamed ledger
/// row, or a cell someone typed by hand would slip through.
/// </summary>
public class LedgerResolutionAuditTests
{
    private const string LEDGER_HEADER =
        "| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-16 | FR-17 | FR-22 |\n" +
        "|---|---|---|---|---|---|---|---|---|---|---|---|\n";

    // Every column Pass — the shape of a row that resolves cleanly.
    private const string ALL_PASS = "Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass";

    // ── 1. Synthetic cases ────────────────────────────────────────────────────

    [Fact]
    public void When_a_ledger_key_does_not_resolve_to_a_row_should_fail_audit()
    {
        // Arrange — the configuration cites a row the ledger does not have, as a misspelling or a
        // renamed row would. GetSkip returns empty for every column, so nothing is skipped and
        // nothing complains.
        using var tree = SyntheticLedgerTree.Create(
            declaredLedgerKey: "Canary / CanaryGatway",
            ledgerRowKey: "Canary / CanaryGateway",
            cellValues: ALL_PASS);

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = Assert.Single(result.Violations);
        Assert.Equal("UnresolvedLedgerKey", violation.Kind);
        Assert.Contains("Canary / CanaryGatway", violation.Detail);
    }

    [Fact]
    public void When_a_canonical_fr_column_is_absent_from_the_matrix_should_fail_audit()
    {
        // Arrange — FR-16 has gone from the header, so every FR-16 cell resolves to nothing
        using var tree = SyntheticLedgerTree.Create(
            declaredLedgerKey: "Canary / CanaryGateway",
            ledgerRowKey: "Canary / CanaryGateway",
            cellValues: "Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass",
            header:
                "| Configuration | FR-2 | FR-4 | FR-5 | FR-6 | FR-7 | FR-8 | FR-9 | FR-15 | FR-17 | FR-22 |\n" +
                "|---|---|---|---|---|---|---|---|---|---|---|\n");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = Assert.Single(result.Violations);
        Assert.Equal("MissingFrColumn", violation.Kind);
        Assert.Contains("FR-16", violation.Detail);
    }

    [Fact]
    public void When_a_cell_carries_an_unrecognised_value_should_fail_audit()
    {
        // Arrange — a hand-typed cell that is none of Pass, Fixed, Unknown or "Deferred ->".
        // ComputeSkip falls through to the empty string, so the behaviour silently runs.
        using var tree = SyntheticLedgerTree.Create(
            declaredLedgerKey: "Canary / CanaryGateway",
            ledgerRowKey: "Canary / CanaryGateway",
            cellValues: "Pass | Pass | Pass | Pass | Pass | Pass | Pass | Pass | probably fine | Pass | Pass");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = Assert.Single(result.Violations);
        Assert.Equal("UnrecognisedCellValue", violation.Kind);
        Assert.Contains("probably fine", violation.Detail);
    }

    [Fact]
    public void When_a_wired_gateway_declares_no_ledger_key_should_fail_audit()
    {
        // Arrange — a configuration with no LedgerKey at all drops out of the cell-agreement audit
        // entirely, taking its whole canonical suite with it and leaving no trace
        using var tree = SyntheticLedgerTree.Create(
            declaredLedgerKey: null,
            ledgerRowKey: "Canary / CanaryGateway",
            cellValues: ALL_PASS);

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = Assert.Single(result.Violations);
        Assert.Equal("MissingLedgerKey", violation.Kind);
        Assert.Equal(0, result.ConfigurationsResolved);
    }

    [Fact]
    public void When_every_declared_key_resolves_should_report_no_violation()
    {
        // Arrange — the configuration cites the row the ledger actually has
        using var tree = SyntheticLedgerTree.Create(
            declaredLedgerKey: "Canary / CanaryGateway",
            ledgerRowKey: "Canary / CanaryGateway",
            cellValues: ALL_PASS);

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(tree.RepoRoot, tree.LedgerPath);

        // Assert — and the run was not vacuous: it resolved the configuration and read its cells
        Assert.Empty(result.Violations);
        Assert.Equal(1, result.ConfigurationsResolved);
        Assert.Equal(CanonicalBehaviours.TEMPLATE_FR_COLUMNS.Values.Distinct().Count(), result.CellsChecked);
    }

    // ── 2. Live-tree fact ─────────────────────────────────────────────────────

    [Fact]
    public void When_checking_the_real_tree_every_declared_ledger_key_should_resolve_to_a_parseable_row()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root from AppContext.BaseDirectory.");
        var ledgerPath = Path.Combine(
            repoRoot, "specs", "0036-universal-transport-conformance-tests", "conformance-status.md");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckLedgerResolution(repoRoot, ledgerPath);

        // Assert — every wired configuration is accounted for, so the cell-agreement audit that
        // shares this lookup cannot be silently checking fewer than it appears to
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, result.ConfigurationsResolved);

        Assert.True(result.Violations.Count == 0,
            "Configuration(s) whose ledger lookup fails open — the lookup returns \"no Skip\" for " +
            "an absent row, an absent column or an unrecognised cell, so these would run unskipped " +
            "with nothing reporting it:\n" +
            string.Join("\n", result.Violations.Select(v => $"  [{v.Kind}] {v.Detail}")));
    }

    // Every wired configuration the ledger is expected to carry a row for. Kept in step with
    // When_generating_everywhere_should_emit_skipped_canonical_suite_in_all_wired_projects.
    private const int EXPECTED_WIRED_CONFIGURATION_COUNT = 24;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class SyntheticLedgerTree : IDisposable
    {
        public string RepoRoot { get; private init; } = string.Empty;
        public string LedgerPath { get; private init; } = string.Empty;

        public static SyntheticLedgerTree Create(
            string? declaredLedgerKey,
            string ledgerRowKey,
            string cellValues,
            string? header = null)
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), $"ledger-resolution-{Guid.NewGuid():N}");
            var projectDir = Path.Combine(repoRoot, "tests", "Paramore.Brighter.Canary.Tests");
            var ledgerDir = Path.Combine(repoRoot, "specs", "0036-universal-transport-conformance-tests");

            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(ledgerDir);

            var ledgerKeyProperty = declaredLedgerKey == null
                ? string.Empty
                : $"\"LedgerKey\": \"{declaredLedgerKey}\"";

            File.WriteAllText(
                Path.Combine(projectDir, "test-configuration.json"),
                $$"""
                  {
                    "Namespace": "Paramore.Brighter.Canary.Tests",
                    "MessagingGateway": {
                      {{ledgerKeyProperty}}
                    }
                  }
                  """);

            var ledgerPath = Path.Combine(ledgerDir, "conformance-status.md");
            File.WriteAllText(ledgerPath, (header ?? LEDGER_HEADER) + $"| {ledgerRowKey} | {cellValues} |\n");

            return new SyntheticLedgerTree { RepoRoot = repoRoot, LedgerPath = ledgerPath };
        }

        public void Dispose() => Directory.Delete(RepoRoot, recursive: true);
    }

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds a directory containing
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c>, which is a reliable marker for the repo root.
    /// </summary>
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "Paramore.Brighter.Kafka.Tests")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
