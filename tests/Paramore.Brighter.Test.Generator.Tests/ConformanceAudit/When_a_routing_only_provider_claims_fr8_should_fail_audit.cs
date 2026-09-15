using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// FR-8 asserts that rejection metadata is stamped onto a rejected message. A transport that
/// dead-letters through a native broker mechanism - RabbitMQ's DLX, Azure Service Bus's own
/// dead-letter queue - routes the untouched original and stamps nothing, so its provider declares
/// that by returning <c>string.Empty</c> for every key in <c>RejectionMetadataKeys</c>. The
/// generated FR-8 test then skips its metadata block and asserts only that the message arrived at
/// the dead-letter destination, which is exactly what FR-4 already asserts.
///
/// That relaxation is maintainer-approved and the ledger prose records it for each transport that
/// uses it. What nothing checked is whether the prose still matches the code. These tests close
/// that gap in two directions:
///
/// <list type="number">
///   <item><description>A provider whose keys are partly filled is a contract violation. The
///   contract is all-empty or all-filled; a mixed set passes the
///   <c>StampsRejectionMetadata</c> guard - which reads only <c>RejectionReason</c> - and then
///   fails deep inside a generated test on <c>ContainsKey("")</c>, naming nothing useful.</description></item>
///   <item><description>A provider that stamps nothing while its FR-8 cell claims Pass or Fixed
///   must be one of the declared relaxations. Otherwise a transport can quietly acquire an FR-8
///   conformance claim that its own test never checks.</description></item>
/// </list>
/// </summary>
public class RejectionMetadataContractAuditTests
{
    // ── 1. Synthetic-tree canaries ────────────────────────────────────────────

    [Fact]
    public void When_a_routing_only_provider_claims_fr8_should_fail_audit()
    {
        // Arrange — stamps nothing, yet the ledger claims FR-8 Pass, and it is not a declared relaxation
        var repo = BuildSyntheticRepo(
            providerKeys: AllEmptyKeys,
            fr8CellValue: "Pass");

        try
        {
            // Act
            var result = RejectionMetadataContractAudit.Audit(repo.RepoRoot, repo.LedgerPath);

            // Assert
            Assert.True(
                result.Violations.Any(v => v.Kind == "UndeclaredRoutingOnly"),
                "Expected an UndeclaredRoutingOnly violation for a provider that stamps no metadata " +
                $"while claiming FR-8 Pass, but got:\n{Format(result)}");
        }
        finally
        {
            Directory.Delete(repo.RepoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_a_routing_only_provider_defers_fr8_should_pass_audit()
    {
        // Arrange — stamps nothing and makes no FR-8 claim, so there is nothing to reconcile
        var repo = BuildSyntheticRepo(
            providerKeys: AllEmptyKeys,
            fr8CellValue: "Deferred -> #4240 (sign-off: @iancooper)");

        try
        {
            // Act
            var result = RejectionMetadataContractAudit.Audit(repo.RepoRoot, repo.LedgerPath);

            // Assert
            Assert.False(
                result.Violations.Any(v => v.Kind == "UndeclaredRoutingOnly"),
                $"A Deferred FR-8 cell claims nothing, so it needs no relaxation:\n{Format(result)}");
        }
        finally
        {
            Directory.Delete(repo.RepoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_a_provider_fills_only_some_rejection_keys_should_fail_audit()
    {
        // Arrange — RejectionReason is filled, so StampsRejectionMetadata says "stamps", but
        // OriginalTopic is empty and the generated test would look up Bag[""]
        var repo = BuildSyntheticRepo(
            providerKeys: """
                          string.Empty,
                                  "originalMessageType",
                                  "rejectionReason",
                                  "rejectionMessage",
                                  "rejectionTimestamp"
                          """,
            fr8CellValue: "Pass");

        try
        {
            // Act
            var result = RejectionMetadataContractAudit.Audit(repo.RepoRoot, repo.LedgerPath);

            // Assert
            Assert.True(
                result.Violations.Any(v => v.Kind == "MixedRejectionKeys"),
                $"Expected a MixedRejectionKeys violation for a partly-filled key set:\n{Format(result)}");
        }
        finally
        {
            Directory.Delete(repo.RepoRoot, recursive: true);
        }
    }

    [Fact]
    public void When_a_provider_stamps_every_rejection_key_should_pass_audit()
    {
        // Arrange — the ordinary case: a transport that really does stamp Brighter metadata
        var repo = BuildSyntheticRepo(
            providerKeys: AllFilledKeys,
            fr8CellValue: "Pass");

        try
        {
            // Act
            var result = RejectionMetadataContractAudit.Audit(repo.RepoRoot, repo.LedgerPath);

            // Assert
            Assert.Empty(result.Violations);
        }
        finally
        {
            Directory.Delete(repo.RepoRoot, recursive: true);
        }
    }

    // ── 2. Live-tree fact ─────────────────────────────────────────────────────

    [Fact]
    public void When_auditing_the_real_tree_every_routing_only_provider_should_be_a_declared_relaxation()
    {
        // Arrange
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Could not locate the repo root.");

        var ledgerPath = Path.Combine(repoRoot, "specs",
            "0036-universal-transport-conformance-tests", "conformance-status.md");

        // Act
        var result = RejectionMetadataContractAudit.Audit(repoRoot, ledgerPath);

        // Assert — non-vacuity: the scan must actually have found providers, and some of them must
        // stamp nothing, or this fact would pass against an empty sweep.
        Assert.True(result.ProvidersScanned > 0,
            "The audit found no gateway providers to scan - the discovery path is wrong.");

        Assert.True(result.RoutingOnlyProvidersFound > 0,
            "The audit found no routing-only providers. RMQ and Azure Service Bus dead-letter " +
            "natively and stamp nothing, so finding none means the key extraction is broken.");

        // Assert — prose and code agree
        Assert.True(result.Violations.Count == 0,
            $"{result.Violations.Count} rejection-metadata contract violation(s):\n{Format(result)}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private const string AllEmptyKeys = """
                                        string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty,
                                                string.Empty
                                        """;

    private const string AllFilledKeys = """
                                         "originalTopic",
                                                 "originalMessageType",
                                                 "rejectionReason",
                                                 "rejectionMessage",
                                                 "rejectionTimestamp"
                                         """;

    private static string Format(RejectionContractResult result) =>
        result.Violations.Count == 0
            ? "  (no violations)"
            : string.Join("\n", result.Violations.Select(v => $"  [{v.Kind}] {v.LedgerKey}: {v.Detail}"));

    /// <summary>
    /// Builds a synthetic repo holding one test project whose configuration declares one gateway,
    /// a provider file returning <paramref name="providerKeys"/>, and a ledger whose FR-8 cell for
    /// that gateway is <paramref name="fr8CellValue"/>.
    /// </summary>
    private static (string RepoRoot, string LedgerPath) BuildSyntheticRepo(
        string providerKeys,
        string fr8CellValue)
    {
        var repoRoot = Path.Combine(
            Path.GetTempPath(), $"rejection-contract-canary-{Guid.NewGuid():N}");

        const string ledgerKey = "Canary / Config";
        const string providerType = "CanaryMessageGatewayProvider";

        var projectDir = Path.Combine(repoRoot, "tests", "Paramore.Brighter.Canary.Tests");
        var gatewayDir = Path.Combine(projectDir, "MessagingGateway");
        Directory.CreateDirectory(gatewayDir);

        File.WriteAllText(Path.Combine(projectDir, "test-configuration.json"), $$"""
            {
              "Namespace": "Paramore.Brighter.Canary.Tests",
              "MessagingGateway": {
                "MessageGatewayProvider": "Paramore.Brighter.Canary.Tests.MessagingGateway.{{providerType}}",
                "LedgerKey": "{{ledgerKey}}"
              }
            }
            """);

        File.WriteAllText(Path.Combine(gatewayDir, $"{providerType}.cs"), $$"""
            namespace Paramore.Brighter.Canary.Tests.MessagingGateway;

            public sealed class {{providerType}}
            {
                public RejectionMetadataKeys RejectionMetadataKeys =>
                    new RejectionMetadataKeys(
                        {{providerKeys}}
                    );
            }
            """);

        var specDir = Path.Combine(repoRoot, "specs", "0036-universal-transport-conformance-tests");
        Directory.CreateDirectory(specDir);
        var ledgerPath = Path.Combine(specDir, "conformance-status.md");
        File.WriteAllText(ledgerPath,
            "## Conformance Matrix\n\n" +
            "| Configuration | FR-2 | FR-4 | FR-8 |\n" +
            "|---|---|---|---|\n" +
            $"| {ledgerKey} | Pass | Pass | {fr8CellValue} |\n");

        return (repoRoot, ledgerPath);
    }

    private static string? FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "Paramore.Brighter.Kafka.Tests")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}
