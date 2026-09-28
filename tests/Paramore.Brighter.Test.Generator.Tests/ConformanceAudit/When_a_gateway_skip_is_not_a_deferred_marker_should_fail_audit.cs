using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit test enforcing the greppable linked-issue Skip convention (ADR 0067).
///
/// Any messaging-gateway test (template or generated copy) whose Skip value does not match
/// the required "Deferred: #&lt;n&gt;" pattern is a CI failure. A bare or reasonless Skip is not
/// acceptable — it is a silent deferral that has no auditable issue link.
///
/// Two kinds of tests live here:
///   1. Synthetic cases — pure predicate tests on known conforming and violating values.
///   2. Live-tree fact — scans the actual in-tree artifacts and asserts zero violations.
/// </summary>
public class GatewaySkipConventionAuditTests
{
    // ── 1. Synthetic cases ────────────────────────────────────────────────────

    [Fact]
    public void When_skip_value_is_deferred_with_real_digits_should_be_conforming()
    {
        // Arrange
        const string conformingValue = "Deferred: #1234 — behaviour not yet conformant for Transport / Gateway (maintainer sign-off)";

        // Act
        var isConforming = GatewaySkipConventionAudit.IsConformingSkipValue(conformingValue);

        // Assert
        Assert.True(isConforming, $"Expected '{conformingValue}' to be a conforming Deferred marker.");
    }

    [Fact]
    public void When_skip_value_is_bare_reason_should_fail_audit()
    {
        // Arrange
        const string bareValue = "flaky";

        // Act
        var isConforming = GatewaySkipConventionAudit.IsConformingSkipValue(bareValue);

        // Assert
        Assert.False(isConforming, $"Expected '{bareValue}' to fail the Deferred marker audit.");
    }

    [Fact]
    public void When_skip_value_is_empty_should_fail_audit()
    {
        // Arrange
        const string emptyValue = "";

        // Act
        var isConforming = GatewaySkipConventionAudit.IsConformingSkipValue(emptyValue);

        // Assert
        Assert.False(isConforming, $"Expected an empty Skip value to fail the Deferred marker audit.");
    }

    [Fact]
    public void When_skip_value_is_deferred_with_placeholder_NNNN_not_digits_should_fail_audit()
    {
        // Arrange — NNNN is the template placeholder used before reconciliation; real digits required
        const string placeholderValue = "Deferred: #NNNN — behaviour not yet conformant for Transport (maintainer sign-off)";

        // Act
        var isConforming = GatewaySkipConventionAudit.IsConformingSkipValue(placeholderValue);

        // Assert
        Assert.False(isConforming, $"Expected '{placeholderValue}' (NNNN placeholder) to fail the Deferred marker audit.");
    }

    [Fact]
    public void When_skip_value_says_deferred_but_has_no_issue_number_should_fail_audit()
    {
        // Arrange
        const string noNumberValue = "Deferred: no number";

        // Act
        var isConforming = GatewaySkipConventionAudit.IsConformingSkipValue(noNumberValue);

        // Assert
        Assert.False(isConforming, $"Expected '{noNumberValue}' to fail the Deferred marker audit.");
    }

    // ── 2. Live-tree fact ─────────────────────────────────────────────────────

    [Fact]
    public void When_scanning_in_tree_artifacts_every_gateway_skip_should_be_a_deferred_marker()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from AppContext.BaseDirectory. " +
                "Expected to walk up and find 'tests/Paramore.Brighter.Kafka.Tests'.");

        // Act
        var result = GatewaySkipConventionAudit.ScanTree(repoRoot);

        // Assert — non-vacuity: the scan must have found files and at least one conforming Deferred marker
        Assert.True(result.FilesScanned > 0,
            $"Scan found zero files under the in-tree artifact paths — " +
            $"the audit is vacuous. Check that the template and Generated paths still exist under:\n" +
            $"  {Path.Combine(repoRoot, "tools")}\n" +
            $"  {Path.Combine(repoRoot, "tests")}");

        Assert.True(result.ConformingSkipsFound > 0,
            $"Scan found {result.FilesScanned} file(s) but zero conforming 'Deferred: #<n>' markers. " +
            $"Either the generated copies have all been promoted (and this assertion needs updating) " +
            $"or the pattern regex is wrong — there are real Deferred markers in the tree.");

        // Assert — zero violations: every Skip = "..." in a scanned artifact must match Deferred: #<digits>
        Assert.True(result.Violations.Count == 0,
            $"{result.Violations.Count} messaging-gateway Skip violation(s) found " +
            $"(ADR 0067: every Skip must match 'Deferred: #<n>'):\n" +
            string.Join("\n", result.Violations.Select(v =>
                $"  {v.FilePath}:{v.LineNumber}  Skip = \"{v.SkipValue}\"")));
    }

    // ── 3. Scan-level canary ──────────────────────────────────────────────────

    /// <summary>
    /// The live-tree fact above can only ever prove the ABSENCE of violations, so on its own it
    /// would pass just as happily if the scan silently matched nothing. This plants the violations
    /// the audit exists to catch into a synthetic tree and proves the scan reports them — the
    /// permanent, in-CI form of the manual canary.
    /// </summary>
    [Fact]
    public void When_scanning_a_tree_with_non_deferred_skips_should_report_them_as_violations()
    {
        // Arrange — a synthetic repo shaped like the two paths ScanTree walks
        var repoRoot = Path.Combine(Path.GetTempPath(), $"skip-audit-canary-{Guid.NewGuid():N}");
        var templateDir = Path.Combine(
            repoRoot, "tools", "Paramore.Brighter.Test.Generator", "Templates", "MessagingGateway");
        var generatedDir = Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Canary.Tests", "MessagingGateway", "Generated", "Reactor");

        Directory.CreateDirectory(templateDir);
        Directory.CreateDirectory(generatedDir);

        try
        {
            // A conforming marker, so the scan has something legitimate to count alongside the canaries
            File.WriteAllText(
                Path.Combine(templateDir, "conforming.cs.liquid"),
                "[Fact(Skip = \"Deferred: #4240 — not yet conformant\")]\n");

            // Three canaries: a bare reason, the un-reconciled placeholder, and the silent empty skip
            File.WriteAllText(
                Path.Combine(generatedDir, "When_a_canary_is_planted.cs"),
                "[Fact(Skip = \"flaky\")]\n" +
                "[Fact(Skip = \"Deferred: #NNNN — placeholder\")]\n" +
                "[Fact(Skip=\"\")]\n");

            // Act
            var result = GatewaySkipConventionAudit.ScanTree(repoRoot);

            // Assert — the one conforming marker is counted, and all three canaries are reported
            Assert.Equal(2, result.FilesScanned);
            Assert.Equal(1, result.ConformingSkipsFound);
            Assert.Equal(
                new[] { "flaky", "Deferred: #NNNN — placeholder", "" },
                result.Violations.Select(v => v.SkipValue));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds a directory containing
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c>, which is a reliable marker for the repo root.
    /// </summary>
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var marker = Path.Combine(dir.FullName, "tests", "Paramore.Brighter.Kafka.Tests");
            if (Directory.Exists(marker))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
