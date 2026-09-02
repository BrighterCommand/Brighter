using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit tests enforcing the two-direction cross-check between in-tree Deferred Skip markers
/// and conformance-ledger rows (ADR 0067, FR-13, FR-21, AC-13, AC-24).
///
/// Direction 1 — Skip → Ledger: every distinct issue number in a generated/template
/// <c>Skip = "Deferred: #&lt;n&gt; …"</c> must appear in at least one ledger cell as
/// <c>Deferred -> #&lt;n&gt;</c>. A Skip whose number has no such cell is a violation.
///
/// Direction 2 — Ledger → Trail: every ledger cell whose value starts with "Deferred" must carry
/// both a real issue link (<c>#&lt;digits&gt;</c>) and a sign-off token
/// (<c>sign-off: @&lt;name&gt;</c>). A Deferred cell missing either is a violation.
///
/// The audit reads only <c>conformance-status.md</c> and the in-tree test artifacts —
/// no network calls, no subprocess, no tracker queries.
/// </summary>
public class LedgerSkipCrossCheckAuditTests
{
    // ── 1. Pure predicate tests on IsValidDeferredCell ────────────────────────

    [Fact]
    public void When_deferred_cell_has_both_issue_link_and_sign_off_should_be_valid()
    {
        // Arrange
        const string cell = "Deferred -> #4240 (sign-off: @maintainer)";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.True(isValid, $"Expected '{cell}' to be a valid Deferred cell.");
    }

    [Fact]
    public void When_deferred_cell_has_no_issue_link_should_be_invalid()
    {
        // Arrange — sign-off present but no #<digits>
        const string cell = "Deferred (sign-off: @maintainer)";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.False(isValid,
            $"Expected '{cell}' (no issue link) to fail the Deferred cell validation.");
    }

    [Fact]
    public void When_deferred_cell_has_no_sign_off_should_be_invalid()
    {
        // Arrange — issue link present but no sign-off token
        const string cell = "Deferred -> #4240";

        // Act
        var isValid = LedgerSkipCrossCheckAudit.IsValidDeferredCell(cell);

        // Assert
        Assert.False(isValid,
            $"Expected '{cell}' (no sign-off) to fail the Deferred cell validation.");
    }

    // ── 2. Synthetic-tree canaries ────────────────────────────────────────────
    // Each canary builds a minimal temporary repo, asserts the audit REPORTS the target
    // violation, then cleans up — permanent in-CI proof that the scan is not vacuous.

    [Fact]
    public void When_skip_has_no_matching_deferred_ledger_row_should_fail_audit()
    {
        // Arrange — synthetic repo: Skip references issue #9999; ledger only contains #4240
        var repoRoot = BuildSyntheticRepo(
            ledgerCells: ["Deferred -> #4240 (sign-off: @maintainer)"],
            skipValues:  ["Deferred: #9999 — no matching ledger row for this number"]);

        try
        {
            var ledgerPath = SyntheticLedgerPath(repoRoot);

            // Act
            var result = LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, ledgerPath);

            // Assert — exactly the Direction-1 violation for issue #9999
            Assert.True(
                result.Violations.Any(v => v.Kind == "SkipWithoutLedgerRow"),
                $"Expected a SkipWithoutLedgerRow violation for #9999 but got:\n" +
                FormatViolations(result.Violations));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_deferred_ledger_cell_has_no_issue_link_should_fail_audit()
    {
        // Arrange — synthetic repo: Deferred cell carries a sign-off but no #<digits>
        var repoRoot = BuildSyntheticRepo(
            ledgerCells: ["Deferred (sign-off: @maintainer)"],
            skipValues:  []);

        try
        {
            var ledgerPath = SyntheticLedgerPath(repoRoot);

            // Act
            var result = LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, ledgerPath);

            // Assert — exactly the Direction-2 violation for a missing issue link
            Assert.True(
                result.Violations.Any(v => v.Kind == "LedgerMissingField"),
                $"Expected a LedgerMissingField violation for a Deferred cell without an issue link but got:\n" +
                FormatViolations(result.Violations));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_deferred_ledger_cell_has_no_sign_off_should_fail_audit()
    {
        // Arrange — synthetic repo: Deferred cell has an issue link but no sign-off token
        var repoRoot = BuildSyntheticRepo(
            ledgerCells: ["Deferred -> #4240"],
            skipValues:  []);

        try
        {
            var ledgerPath = SyntheticLedgerPath(repoRoot);

            // Act
            var result = LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, ledgerPath);

            // Assert — exactly the Direction-2 violation for a missing sign-off
            Assert.True(
                result.Violations.Any(v => v.Kind == "LedgerMissingField"),
                $"Expected a LedgerMissingField violation for a Deferred cell without a sign-off but got:\n" +
                FormatViolations(result.Violations));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── 3. Live-tree fact ─────────────────────────────────────────────────────

    [Fact]
    public void When_cross_checking_live_tree_against_ledger_every_skip_should_have_a_matching_ledger_row()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from AppContext.BaseDirectory. " +
                "Expected to walk up and find 'tests/Paramore.Brighter.Kafka.Tests'.");

        var ledgerPath = Path.Combine(repoRoot, "specs",
            "0036-universal-transport-conformance-tests", "conformance-status.md");

        Assert.True(File.Exists(ledgerPath),
            $"Conformance ledger not found at expected path:\n  {ledgerPath}");

        // Act
        var result = LedgerSkipCrossCheckAudit.CrossCheck(repoRoot, ledgerPath);

        // Assert — non-vacuity: the parser must have found matrix rows and Deferred cells
        Assert.True(result.LedgerDataRowsFound > 0,
            $"The ledger parser found no matrix data rows — either the parser is broken " +
            $"or the '| Configuration |' header row has moved. Ledger: {ledgerPath}");

        Assert.True(result.DeferredCellsFound > 0,
            $"The ledger parser found {result.LedgerDataRowsFound} data row(s) but zero " +
            $"Deferred cells. The real ledger has many Deferred cells — the parser regex " +
            $"is likely wrong, or all deferrals have been resolved (unlikely at this stage).");

        Assert.True(result.DistinctSkipIssueNumbers > 0,
            $"The tree scan found no 'Deferred: #<n>' Skip markers in the in-tree generated " +
            $"or template artifacts. Either the scan paths are wrong or all deferred tests have " +
            $"been promoted — in which case this assertion should be updated.");

        // Assert — both directions produce zero violations
        Assert.True(result.Violations.Count == 0,
            $"{result.Violations.Count} cross-check violation(s) found " +
            $"(ADR 0067: every Skip must have a matching Deferred ledger row; every Deferred " +
            $"ledger cell must carry an issue link and a sign-off entry):\n" +
            FormatViolations(result.Violations));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal synthetic repo shaped like the two paths the audit walks, seeding the
    /// ledger with the given cells and (if any) a generated file with the given Skip values.
    /// </summary>
    private static string BuildSyntheticRepo(
        IReadOnlyList<string> ledgerCells,
        IReadOnlyList<string> skipValues)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"ledger-crosscheck-canary-{Guid.NewGuid():N}");

        // Ledger file
        var specDir = Path.Combine(repoRoot, "specs",
            "0036-universal-transport-conformance-tests");
        Directory.CreateDirectory(specDir);

        var dataRows = string.Join(
            "\n", ledgerCells.Select(c => $"| Canary / Config | {c} |"));
        File.WriteAllText(
            Path.Combine(specDir, "conformance-status.md"),
            "## Conformance Matrix\n\n" +
            "| Configuration | FR-2 |\n" +
            "|---|---|\n" +
            dataRows + "\n");

        // Generated directory and file (created only when Skip values are supplied)
        var generatedDir = Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Canary.Tests",
            "MessagingGateway", "Generated", "Reactor");
        Directory.CreateDirectory(generatedDir);

        if (skipValues.Count > 0)
        {
            File.WriteAllText(
                Path.Combine(generatedDir, "When_a_canary_skip.cs"),
                string.Join("\n", skipValues.Select(v => $"[Fact(Skip = \"{v}\")]")) + "\n");
        }

        // Empty template directory so the scan doesn't mis-count missing roots
        Directory.CreateDirectory(Path.Combine(
            repoRoot, "tools", "Paramore.Brighter.Test.Generator",
            "Templates", "MessagingGateway"));

        return repoRoot;
    }

    private static string SyntheticLedgerPath(string repoRoot) =>
        Path.Combine(repoRoot, "specs",
            "0036-universal-transport-conformance-tests", "conformance-status.md");

    private static string FormatViolations(IReadOnlyList<LedgerCrossCheckViolation> violations) =>
        violations.Count == 0
            ? "  (none)"
            : string.Join("\n", violations.Select(v => $"  [{v.Kind}] {v.Detail}"));

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds the directory that contains
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c> — a reliable marker for the repo root.
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
