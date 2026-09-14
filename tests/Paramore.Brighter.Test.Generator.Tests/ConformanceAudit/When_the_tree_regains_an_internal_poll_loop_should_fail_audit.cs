#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Runs the rejection-destination poll-contract audit against the real repository, which is the
/// point of it existing.
/// </summary>
/// <remarks>
/// <para>
/// The canary in <see cref="DeadLetterPollContractAuditTests"/> proves the scan detects an internal
/// retry loop, but it does so against a synthetic tree and so guards nothing. This test points the
/// same scan at the repository itself, and is what fails the build when a provider goes back to
/// retrying inside its own helper.
/// </para>
/// <para>
/// That regression is worth a guard because it is invisible in review: the helper still returns the
/// right message, every conformance cell stays green, and the only symptoms are that the caller's
/// NFR-2 loop quietly stops retrying and the AC-20 absence checks quietly become the slowest tests
/// in the suite.
/// </para>
/// </remarks>
public class RealTreePollContractTests
{
    [Fact]
    public void When_the_tree_regains_an_internal_poll_loop_should_fail_audit()
    {
        // Arrange — the real repository, located the way the other tree-walking tests locate it
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        Assert.True(repoRoot is not null,
            "Could not locate the repository root; the audit cannot guard a tree it cannot find.");

        // Act
        var result = DeadLetterPollContractAudit.Audit(repoRoot!);

        // Assert — the scan reached the providers, and none of them retries internally
        Assert.True(result.HelpersScanned > 0,
            "The audit found no rejection-destination helpers at all. It is passing vacuously, "
            + "which is worse than failing: fix the scan before trusting this test.");

        Assert.True(result.Violations.Count == 0,
            "A rejection-destination helper retries inside itself. That makes the generated test's "
            + "bounded loop decorative - the first call overruns its ceiling, so the loop never "
            + "runs a second iteration - and it makes every AC-20 absence check pay the helper's "
            + "full ceiling on every run, because the message it waits for is asserted never to "
            + "arrive. Attempt one bounded receive and return; leave the retry to the caller.\n"
            + FormatViolations(result.Violations));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from the test assembly looking for a directory that holds the repository's
    /// <c>tests/</c> tree, matching the discovery the other tree-walking tests already use.
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

    private static string FormatViolations(IReadOnlyList<HelperPollViolation> violations) =>
        violations.Count == 0
            ? "  (no violations reported)"
            : string.Join("\n", violations
                .GroupBy(v => v.Provider)
                .Select(g => $"  {g.Key}\n"
                             + string.Join("\n", g.Select(v => $"    {v.Helper}: {v.Kind} - {v.Detail}"))));
}
