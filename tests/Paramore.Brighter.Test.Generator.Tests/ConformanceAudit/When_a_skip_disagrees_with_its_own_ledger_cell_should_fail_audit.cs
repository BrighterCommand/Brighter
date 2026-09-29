using System;
using System.IO;
using System.Linq;
using Paramore.Brighter.Test.Generator;


namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit test for the strongest direction of the deferral trail: a generated test's Skip must
/// agree with ITS OWN ledger cell — the exact (LedgerKey × FR column) intersection — not merely
/// with the ledger as a whole.
///
/// The sibling <see cref="LedgerSkipCrossCheckAudit.CrossCheck"/> matches on issue number alone.
/// Because every deferral currently resolves to the one umbrella issue #4240, that check reduces
/// to "#4240 appears somewhere in the ledger" and cannot see the drift that actually happens:
/// a cell flipped to Pass without regenerating (the test stays skipped), or a cell flipped to
/// Deferred without regenerating (the test keeps running). This closes both.
///
/// Two kinds of tests live here:
///   1. Synthetic cases — a hand-built repo + ledger proving each drift shape is reported.
///   2. Live-tree fact — the real tree agrees with the real ledger, cell for cell.
/// </summary>
public class LedgerCellAgreementAuditTests
{
    // Built from CanonicalBehaviours rather than written out, so that adding a canonical
    // behaviour cannot leave these fixtures describing a ledger the audit no longer expects -
    // which is exactly the drift the audit exists to report.
    private static readonly string[] s_frColumns = CanonicalColumns();

    private static readonly string LEDGER_HEADER =
        "| Configuration | " + string.Join(" | ", s_frColumns) + " |\n"
        + "|---" + string.Concat(Enumerable.Repeat("|---", s_frColumns.Length)) + "|\n";

    private static string[] CanonicalColumns() =>
        CanonicalBehaviours.FR_COLUMN_BEHAVIOURS.Keys
            .OrderBy(column => int.Parse(column.Substring(3)))
            .ToArray();

    // The Nack-redelivers behaviour is column FR-16 of the conformance ledger — the 9th
    // behaviour column in the matrix.
    private const string NACK_TEMPLATE = "When_nacking_a_message_it_should_be_redelivered";

    // ── 1. Synthetic cases ────────────────────────────────────────────────────

    [Test]
    public async System.Threading.Tasks.Task When_a_test_is_skipped_but_its_cell_says_pass_should_report_a_violation()
    {
        // Arrange — the conformance ledger says the Nack-redelivers behaviour (FR-16) passes for
        // this configuration, so no Skip should exist,
        // but the generated file is still carrying one (a cell flipped green without regenerating)
        using var tree = SyntheticTree.Create(
            frColumnValues: AllPass(),
            skipValue: "Deferred: #4240 — Nack redelivers not yet conformant for Canary / CanaryGateway (maintainer sign-off)");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckCellAgreement(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = await Assert.That(result.Violations).HasSingleItem();
        await Assert.That(violation.FrColumn).IsEqualTo("FR-16");
        await Assert.That(violation.ExpectedSkip).IsEqualTo(string.Empty);
        await Assert.That(violation.ActualSkip).Contains("Deferred: #4240");
    }

    [Test]
    public async System.Threading.Tasks.Task When_a_cell_is_deferred_but_its_test_carries_no_skip_should_report_a_violation()
    {
        // Arrange — the conformance ledger defers the Nack-redelivers behaviour (FR-16), so the
        // generated file must carry a Skip; it has none
        // (a cell flipped to Deferred without regenerating, leaving the test running)
        using var tree = SyntheticTree.Create(
            frColumnValues: DeferFr16(),
            skipValue: null);

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckCellAgreement(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = await Assert.That(result.Violations).HasSingleItem();
        await Assert.That(violation.FrColumn).IsEqualTo("FR-16");
        await Assert.That(violation.ExpectedSkip).Contains("Deferred: #4240");
        await Assert.That(violation.ActualSkip).IsEqualTo(string.Empty);
    }

    [Test]
    public async System.Threading.Tasks.Task When_a_skip_cites_a_different_issue_than_its_cell_should_report_a_violation()
    {
        // Arrange — cell defers to #4240 but the Skip cites #9999
        using var tree = SyntheticTree.Create(
            frColumnValues: DeferFr16(),
            skipValue: "Deferred: #9999 — Nack redelivers not yet conformant for Canary / CanaryGateway (maintainer sign-off)");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckCellAgreement(tree.RepoRoot, tree.LedgerPath);

        // Assert
        var violation = await Assert.That(result.Violations).HasSingleItem();
        await Assert.That(violation.ExpectedSkip).Contains("#4240");
        await Assert.That(violation.ActualSkip).Contains("#9999");
    }

    [Test]
    public async System.Threading.Tasks.Task When_a_skip_matches_its_own_deferred_cell_exactly_should_report_no_violation()
    {
        // Arrange — the Skip is exactly what the generator would emit for this cell
        using var tree = SyntheticTree.Create(
            frColumnValues: DeferFr16(),
            skipValue: "Deferred: #4240 — Nack redelivers not yet conformant for Canary / CanaryGateway (maintainer sign-off)");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckCellAgreement(tree.RepoRoot, tree.LedgerPath);

        // Assert
        await Assert.That(result.Violations).IsEmpty();
        await Assert.That(result.FilesChecked).IsEqualTo(1);
    }

    // ── 2. Live-tree fact ─────────────────────────────────────────────────────

    [Test]
    public async System.Threading.Tasks.Task When_checking_the_real_tree_every_generated_skip_should_agree_with_its_own_ledger_cell()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate repo root from AppContext.BaseDirectory.");
        var ledgerPath = Path.Combine(
            repoRoot, "specs", "0036-universal-transport-conformance-tests", "conformance-status.md");

        // Act
        var result = LedgerSkipCrossCheckAudit.CheckCellAgreement(repoRoot, ledgerPath);

        // Assert — non-vacuity: the check must have resolved real configurations and real files,
        // and must have exercised BOTH branches (cells that expect a Skip and cells that do not).
        await Assert.That(result.LedgerKeysResolved > 0).IsTrue().Because("Zero configurations resolved to a Generated directory — the audit is vacuous. " +
            "The generator's output-path convention (MessagingGateway/<prefix>/Generated) has " +
            "probably changed; update EnumerateGeneratedCanonicalFiles to match.");

        await Assert.That(result.FilesChecked > 0).IsTrue().Because($"Resolved {result.LedgerKeysResolved} configuration(s) but checked zero canonical " +
            "generated files — the audit is vacuous.");

        await Assert.That(result.ExpectedSkipCount > 0 && result.ExpectedNoSkipCount > 0).IsTrue().Because($"The check exercised only one branch (expect-Skip: {result.ExpectedSkipCount}, " +
            $"expect-no-Skip: {result.ExpectedNoSkipCount}). Both must occur for this to be a " +
            "real check — the ledger holds both Deferred and Pass/Fixed cells.");

        // Assert — every generated Skip agrees with its own cell, in both directions
        await Assert.That(result.Violations.Count == 0).IsTrue().Because($"{result.Violations.Count} generated test(s) disagree with their own ledger cell " +
            $"(regenerate with ./generate-test.sh, or correct the ledger):\n" +
            string.Join("\n", result.Violations.Select(v =>
                $"  {v.FilePath}\n" +
                $"    cell     {v.LedgerKey} × {v.FrColumn}\n" +
                $"    expected {(v.ExpectedSkip.Length == 0 ? "(no Skip)" : $"\"{v.ExpectedSkip}\"")}\n" +
                $"    actual   {(v.ActualSkip.Length == 0 ? "(no Skip)" : $"\"{v.ActualSkip}\"")}")));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string AllPass() =>
        string.Join(" | ", Enumerable.Repeat("Pass", s_frColumns.Length));

    private static string DeferFr16()
    {
        // Positioned by column name, not a hard-coded index, so the fixture keeps deferring the
        // column it means to when the canonical set changes.
        var cells = Enumerable.Repeat("Pass", s_frColumns.Length).ToArray();
        cells[Array.IndexOf(s_frColumns, "FR-16")] = "Deferred -> #4240 (sign-off: @iancooper)";
        return string.Join(" | ", cells);
    }

    /// <summary>
    /// A throwaway repo shaped exactly as the generator lays one out: a test project with a
    /// test-configuration.json declaring one gateway, and one generated canonical test under
    /// MessagingGateway/Generated/Reactor.
    /// </summary>
    private sealed class SyntheticTree : IDisposable
    {
        public string RepoRoot { get; private init; } = string.Empty;
        public string LedgerPath { get; private init; } = string.Empty;

        public static SyntheticTree Create(string frColumnValues, string? skipValue)
        {
            var repoRoot = Path.Combine(Path.GetTempPath(), $"cell-agreement-{Guid.NewGuid():N}");
            var projectDir = Path.Combine(repoRoot, "tests", "Paramore.Brighter.Canary.Tests");
            var generatedDir = Path.Combine(projectDir, "MessagingGateway", "Generated", "Reactor");
            var ledgerDir = Path.Combine(repoRoot, "specs", "0036-universal-transport-conformance-tests");

            Directory.CreateDirectory(generatedDir);
            Directory.CreateDirectory(ledgerDir);

            File.WriteAllText(
                Path.Combine(projectDir, "test-configuration.json"),
                """
                {
                  "Namespace": "Paramore.Brighter.Canary.Tests",
                  "MessagingGateway": {
                    "LedgerKey": "Canary / CanaryGateway"
                  }
                }
                """);

            var ledgerPath = Path.Combine(ledgerDir, "conformance-status.md");
            File.WriteAllText(ledgerPath, LEDGER_HEADER + $"| Canary / CanaryGateway | {frColumnValues} |\n");

            var skipAttribute = skipValue == null ? "[Test]" : $"[Test][Skip(\"{skipValue}\")]";
            File.WriteAllText(
                Path.Combine(generatedDir, $"{NACK_TEMPLATE}.cs"),
                $"public class Canary\n{{\n    {skipAttribute}\n    public void Run() {{ }}\n}}\n");

            return new SyntheticTree { RepoRoot = repoRoot, LedgerPath = ledgerPath };
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
