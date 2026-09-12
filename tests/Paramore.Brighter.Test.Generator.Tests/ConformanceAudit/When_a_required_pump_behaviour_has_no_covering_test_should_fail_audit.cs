using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit tests enforcing that the pump-side half of the conformance claim still exists.
/// </summary>
/// <remarks>
/// <para>
/// The conformance ledger's validity is compositional. The gateway suite proves that a transport
/// honours a requeue or a reject; it does not drive a pump, and deliberately so. The pump's half —
/// deciding *when* to requeue or reject — is proven separately against in-memory channels in
/// <c>tests/Paramore.Brighter.Core.Tests/MessageDispatch</c>.
/// </para>
/// <para>
/// Nothing otherwise links the two halves. Delete or narrow those pump tests and every cell in
/// <c>conformance-status.md</c> stays green while the composite claim quietly stops being true,
/// because the gateway suite never asserted the pump's half in the first place. This audit is that
/// missing link: it fails when a pump behaviour the ledger depends on has no covering test.
/// </para>
/// </remarks>
public class PumpCoverageAuditTests
{
    [Fact]
    public void When_a_required_pump_behaviour_has_no_covering_test_should_fail_audit()
    {
        // Arrange — a synthetic MessageDispatch tree covering every required pump behaviour
        // except the requeue-count threshold, which is the half FR-23's ledger cells lean on.
        var omitted = "requeue count threshold reached";
        var repoRoot = BuildSyntheticRepo(omitting: omitted);

        try
        {
            // Act
            var result = PumpCoverageAudit.CheckCoverage(repoRoot);

            // Assert — the audit names the uncovered behaviour rather than merely failing
            Assert.True(
                result.Violations.Any(v =>
                    v.Kind == "PumpBehaviourNotCovered" && v.Behaviour == omitted),
                $"Expected a PumpBehaviourNotCovered violation for '{omitted}' but got:\n"
                + FormatViolations(result.Violations));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal repo whose MessageDispatch tree carries one file per required pump
    /// behaviour, except the one named in <paramref name="omitting"/>.
    /// </summary>
    private static string BuildSyntheticRepo(string omitting)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"pump-coverage-canary-{Guid.NewGuid():N}");

        var dispatchDir = Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Core.Tests", "MessageDispatch", "Reactor");
        Directory.CreateDirectory(dispatchDir);

        foreach (var behaviour in PumpCoverageAudit.RequiredBehaviours)
        {
            if (behaviour.Description == omitting)
            {
                continue;
            }

            File.WriteAllText(
                Path.Combine(dispatchDir, $"{behaviour.ExampleTestName}.cs"),
                "// synthetic canary file\n");
        }

        return repoRoot;
    }

    private static string FormatViolations(IReadOnlyList<PumpCoverageViolation> violations) =>
        violations.Count == 0
            ? "  (no violations reported)"
            : string.Join("\n", violations.Select(v => $"  {v.Kind}: {v.Behaviour} — {v.Detail}"));
}
