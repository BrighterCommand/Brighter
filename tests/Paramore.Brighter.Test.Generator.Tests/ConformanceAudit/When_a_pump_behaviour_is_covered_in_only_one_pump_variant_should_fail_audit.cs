using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Audit tests enforcing Reactor/Proactor parity on the pump half of the conformance claim.
/// </summary>
/// <remarks>
/// <para>
/// Every canonical behaviour must be proven in both the Reactor and the Proactor
/// variant, and the ledger's ✅ cells are read that way. The pump half is proven in
/// <c>tests/Paramore.Brighter.Core.Tests/MessageDispatch</c>, which carries a <c>Reactor</c> and a
/// <c>Proactor</c> tree for exactly that reason.
/// </para>
/// <para>
/// A behaviour covered in only one of those trees satisfies half of what the ledger claims. The
/// gateway suite cannot notice — it never asserted the pump's half — so the composite claim would
/// quietly narrow to one pump while every cell stayed green. This test is the guard against that.
/// </para>
/// </remarks>
public class PumpVariantParityAuditTests
{
    [Fact]
    public void When_a_pump_behaviour_is_covered_in_only_one_pump_variant_should_fail_audit()
    {
        // Arrange — a MessageDispatch tree carrying both pump variants for every required
        // behaviour, except that the requeue-count threshold has lost its Proactor half
        var strandedInReactor = "requeue count threshold reached";
        var repoRoot = BuildSyntheticRepo(withoutProactorHalfOf: strandedInReactor);

        try
        {
            // Act
            var result = PumpCoverageAudit.CheckCoverage(repoRoot);

            // Assert — the audit names the behaviour AND the variant that went missing, rather
            // than accepting the surviving Reactor half as coverage
            Assert.True(
                result.Violations.Any(v =>
                    v.Kind == "PumpBehaviourVariantNotCovered"
                    && v.Behaviour == strandedInReactor
                    && v.Detail.Contains("Proactor", StringComparison.Ordinal)),
                $"Expected a PumpBehaviourVariantNotCovered violation naming the missing Proactor "
                + $"half of '{strandedInReactor}' but got:\n"
                + FormatViolations(result.Violations));

            // Assert — the behaviours that kept both halves are not reported
            Assert.DoesNotContain(
                result.Violations,
                v => v.Behaviour != strandedInReactor);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal repo whose MessageDispatch tree carries a Reactor and a Proactor file for
    /// every required pump behaviour, except that the behaviour named in
    /// <paramref name="withoutProactorHalfOf"/> gets its Reactor half only.
    /// </summary>
    private static string BuildSyntheticRepo(string withoutProactorHalfOf)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"pump-variant-parity-canary-{Guid.NewGuid():N}");

        var dispatchDir = Path.Combine(
            repoRoot, "tests", "Paramore.Brighter.Core.Tests", "MessageDispatch");

        var reactorDir = Path.Combine(dispatchDir, "Reactor");
        var proactorDir = Path.Combine(dispatchDir, "Proactor");
        Directory.CreateDirectory(reactorDir);
        Directory.CreateDirectory(proactorDir);

        foreach (var behaviour in PumpCoverageAudit.RequiredBehaviours)
        {
            WriteCoveringTest(reactorDir, behaviour.ExampleTestName);

            if (behaviour.Description != withoutProactorHalfOf)
            {
                WriteCoveringTest(proactorDir, $"{behaviour.ExampleTestName}_async");
            }
        }

        return repoRoot;
    }

    /// <summary>
    /// Writes a canary test file substantial enough that only the missing variant, and nothing
    /// about the file's contents, can be what the audit objects to.
    /// </summary>
    private static void WriteCoveringTest(string directory, string testName) =>
        File.WriteAllText(
            Path.Combine(directory, $"{testName}.cs"),
            $$"""
              public class {{"Canary"}}Tests
              {
                  [Fact]
                  public void {{testName}}()
                  {
                      Assert.True(true);
                  }
              }
              """);

    private static string FormatViolations(IReadOnlyList<PumpCoverageViolation> violations) =>
        violations.Count == 0
            ? "  (no violations reported)"
            : string.Join("\n", violations.Select(v => $"  {v.Kind}: {v.Behaviour} — {v.Detail}"));
}
