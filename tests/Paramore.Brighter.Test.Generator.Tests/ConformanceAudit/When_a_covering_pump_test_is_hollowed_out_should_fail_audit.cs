using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit tests enforcing that a pump test the ledger leans on still asserts something.
/// </summary>
/// <remarks>
/// <para>
/// Matching a file name proves a test has not been deleted or moved. It does not prove the test
/// still does anything: strip the assertions out of a covering test and it keeps its name, keeps
/// passing, and stops proving the pump's half of the claim. Every conformance cell stays green
/// while the composite claim quietly becomes false — the same failure as deletion, wearing the
/// name of the test it replaced.
/// </para>
/// <para>
/// So a covering file must still assert. The bar is deliberately the lowest one that distinguishes
/// a test from an empty method, because the audit cannot judge whether an assertion is a
/// <em>good</em> one; that remains a code-review concern. What it can do is refuse to count a test
/// body with nothing in it.
/// </para>
/// </remarks>
public class HollowedOutPumpTestAuditTests
{
    [Fact]
    public void When_a_covering_pump_test_is_hollowed_out_should_fail_audit()
    {
        // Arrange — every required behaviour covered in both pump variants, except that the
        // Proactor half of the requeue-count threshold kept its name and its [Fact] while its
        // assertions were taken out
        var hollowed = "requeue count threshold reached";
        var repoRoot = BuildSyntheticRepo(hollowingTheProactorHalfOf: hollowed);

        try
        {
            // Act
            var result = PumpCoverageAudit.CheckCoverage(repoRoot);

            // Assert — the audit names the hollowed test rather than counting its file name as
            // coverage, and says which file to go and look at
            Assert.True(
                result.Violations.Any(v =>
                    v.Kind == "PumpBehaviourCoverageHollowedOut"
                    && v.Behaviour == hollowed
                    && v.Detail.Contains("Proactor", StringComparison.Ordinal)),
                $"Expected a PumpBehaviourCoverageHollowedOut violation for '{hollowed}' but got:\n"
                + FormatViolations(result.Violations));

            // Assert — the behaviours whose tests still assert are not reported
            Assert.DoesNotContain(result.Violations, v => v.Behaviour != hollowed);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal repo whose MessageDispatch tree carries an asserting test in both pump
    /// variants for every required behaviour, except that the Proactor half of the behaviour named
    /// in <paramref name="hollowingTheProactorHalfOf"/> is left with its name and no assertions.
    /// </summary>
    private static string BuildSyntheticRepo(string hollowingTheProactorHalfOf)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"pump-hollowed-canary-{Guid.NewGuid():N}");

        var dispatchDir = Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Core.Tests", "MessageDispatch");

        var reactorDir = Path.Combine(dispatchDir, "Reactor");
        var proactorDir = Path.Combine(dispatchDir, "Proactor");
        Directory.CreateDirectory(reactorDir);
        Directory.CreateDirectory(proactorDir);

        foreach (var behaviour in PumpCoverageAudit.RequiredBehaviours)
        {
            var proactorName = $"{behaviour.ExampleTestName}_async";

            WriteTest(reactorDir, behaviour.ExampleTestName, asserting: true);
            WriteTest(proactorDir, proactorName,
                asserting: behaviour.Description != hollowingTheProactorHalfOf);
        }

        return repoRoot;
    }

    /// <summary>
    /// Writes a canary test file that either asserts or has had its assertions removed while
    /// keeping its name and its <c>[Fact]</c> — the shape a test hollowed out in place actually
    /// takes.
    /// </summary>
    private static void WriteTest(string directory, string testName, bool asserting) =>
        File.WriteAllText(
            Path.Combine(directory, $"{testName}.cs"),
            "public class CanaryTests\n"
            + "{\n"
            + "    [Fact]\n"
            + $"    public void {testName}()\n"
            + "    {\n"
            + (asserting ? "        Assert.True(true);\n" : "        // assertions removed\n")
            + "    }\n"
            + "}\n");

    private static string FormatViolations(IReadOnlyList<PumpCoverageViolation> violations) =>
        violations.Count == 0
            ? "  (no violations reported)"
            : string.Join("\n", violations.Select(v => $"  {v.Kind}: {v.Behaviour} — {v.Detail}"));
}
