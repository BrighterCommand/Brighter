using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Runs the pump-coverage audit against the real repository, which is the point of it existing.
/// </summary>
/// <remarks>
/// <para>
/// The canary in <see cref="PumpCoverageAuditTests"/> proves the scan detects a missing behaviour,
/// but it does so against a synthetic tree and so guards nothing. This test points the same scan at
/// the repository itself, and is what actually fails the build when someone deletes or moves the
/// <c>MessageDispatch</c> tests the conformance ledger leans on.
/// </para>
/// <para>
/// It is expected to be green on arrival. Its value is entirely in the day it goes red.
/// </para>
/// </remarks>
public class RealTreePumpCoverageTests
{
    [Fact]
    public void When_the_tree_loses_a_pump_behaviour_the_ledger_depends_on_should_fail_audit()
    {
        // Arrange — the real repository, located the way the other tree-walking tests locate it
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.True(repoRoot is not null,
            "Could not locate the repository root; the audit cannot guard a tree it cannot find.");

        // Act
        var result = PumpCoverageAudit.CheckCoverage(repoRoot!);

        // Assert — every pump behaviour the ledger's "division of labour" cites is still covered
        Assert.True(result.Violations.Count == 0,
            "The conformance ledger's validity is compositional: its gateway suite proves the "
            + "transport, and Core.Tests/MessageDispatch proves the pump. A behaviour below has "
            + "lost its covering test, so ledger cells now assert more than the tree proves. "
            + "Restore the test, or amend both PumpCoverageAudit.RequiredBehaviours and the "
            + "\"division of labour\" section of conformance-status.md to match reality.\n"
            + FormatViolations(result.Violations));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from the test assembly looking for a directory that holds the repository's
    /// <c>tests/</c> tree, matching the discovery the canonical-template tests already use.
    /// </summary>
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var marker = Path.Combine(dir.FullName, "tests", "Paramore.Brighter.Core.Tests");
            if (Directory.Exists(marker))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string FormatViolations(IReadOnlyList<PumpCoverageViolation> violations) =>
        violations.Count == 0
            ? "  (no violations reported)"
            : string.Join("\n", violations.Select(v => $"  {v.Behaviour}\n    {v.Detail}"));
}
