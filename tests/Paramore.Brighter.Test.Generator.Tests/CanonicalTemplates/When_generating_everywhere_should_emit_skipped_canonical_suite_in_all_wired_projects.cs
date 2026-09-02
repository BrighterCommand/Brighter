using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Structural integration gate for FR-13 (AC-13), FR-14, and FR-21 (ADR 0067 stage (i)).
///
/// After a full regeneration with `./generate-test.sh`, every wired gateway configuration's
/// Generated/Reactor and Generated/Proactor directories must contain the complete canonical
/// suite, and each canonical test must carry the ledger-driven Deferred Skip IFF its
/// conformance-ledger cell is not yet Pass/Fixed (the Skip marker being #NNNN for an Unknown
/// cell or a real #&lt;n&gt; for a Deferred cell). The expectation is derived from the same
/// ledger the generator reads, so this gate stays correct as the rollout proves behaviours.
///
/// What this test does NOT prove (separate concerns):
/// - That the generated tests compile   — the solution build is the gate for that.
/// - That the generated tests pass      — that requires a live broker and is the fix phase.
///
/// References: FR-13, AC-13, FR-14, FR-21, ADR 0067 "generate everywhere immediately", stage (i).
/// </summary>
public class GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
{
    // The eleven canonical template base names — one per canonical behaviour.
    // FR-2, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-15, FR-16, FR-17, FR-22 (FR-21).
    private static readonly string[] CANONICAL_TEMPLATE_NAMES =
    [
        "When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay",               // FR-2
        "When_rejecting_message_with_delivery_error_should_send_to_dlq",                          // FR-4
        "When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel",         // FR-5
        "When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq", // FR-6
        "When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log",           // FR-7
        "When_rejecting_message_should_include_metadata",                                          // FR-8
        "When_sending_a_delayed_message_should_deliver_after_delay",                               // FR-9
        "When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately",            // FR-15
        "When_nacking_a_message_it_should_be_redelivered",                                         // FR-16
        "When_rejecting_message_with_unknown_reason_should_send_to_dlq",                           // FR-17
        "When_requeuing_a_failed_message_should_be_redelivered",                                   // FR-22
    ];

    // Canonical template base name → conformance-ledger FR column, mirroring the generator's
    // authoritative CANONICAL_TEMPLATE_FR_COLUMNS map (FR-21 / ADR 0067). Used to resolve the
    // ledger cell a given generated file must agree with.
    private static readonly IReadOnlyDictionary<string, string> TEMPLATE_FR_COLUMNS =
        new Dictionary<string, string>
        {
            ["When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay"]                = "FR-2",
            ["When_rejecting_message_with_delivery_error_should_send_to_dlq"]                          = "FR-4",
            ["When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel"]         = "FR-5",
            ["When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq"] = "FR-6",
            ["When_rejecting_message_with_no_channels_configured_should_acknowledge_and_log"]          = "FR-7",
            ["When_rejecting_message_should_include_metadata"]                                          = "FR-8",
            ["When_sending_a_delayed_message_should_deliver_after_delay"]                              = "FR-9",
            ["When_requeuing_a_failed_message_with_zero_delay_should_redeliver_immediately"]           = "FR-15",
            ["When_nacking_a_message_it_should_be_redelivered"]                                         = "FR-16",
            ["When_rejecting_message_with_unknown_reason_should_send_to_dlq"]                          = "FR-17",
            ["When_requeuing_a_failed_message_should_be_redelivered"]                                  = "FR-22",
        };

    // The exact count of wired gateway configurations declared across the ten wired
    // test projects (FR-13). This is a regression guard: a new wiring changes the count.
    private const int EXPECTED_WIRED_CONFIGURATION_COUNT = 23;

    private readonly string _repoRoot;
    private readonly IReadOnlyList<string> _reactorGeneratedDirs;
    private readonly IReadOnlyList<string> _proactorGeneratedDirs;

    public GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests()
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from AppContext.BaseDirectory. " +
                "Expected to walk up and find 'tests/Paramore.Brighter.Kafka.Tests'.");

        var testsRoot = Path.Combine(_repoRoot, "tests");

        _reactorGeneratedDirs = FindGeneratedVariantDirs(testsRoot, "Reactor");
        _proactorGeneratedDirs = FindGeneratedVariantDirs(testsRoot, "Proactor");
    }

    [Fact]
    public void When_generating_everywhere_should_find_exactly_twenty_wired_configurations()
    {
        // Arrange — resolved in constructor

        // Assert — both variants must agree on the configuration count (FR-13).
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _reactorGeneratedDirs.Count);
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _proactorGeneratedDirs.Count);
    }

    [Fact]
    public void When_generating_everywhere_all_wired_reactor_directories_should_contain_canonical_suite()
    {
        // Arrange — resolved in constructor

        // Assert — every wired Generated/Reactor directory contains the full canonical suite (AC-13, FR-14)
        var missing = new List<string>();

        foreach (var dir in _reactorGeneratedDirs)
        {
            foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    missing.Add($"  Reactor: {filePath}");
            }
        }

        Assert.True(missing.Count == 0,
            $"Canonical Reactor files absent after regeneration — run ./generate-test.sh and rebuild:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void When_generating_everywhere_all_wired_proactor_directories_should_contain_canonical_suite()
    {
        // Arrange — resolved in constructor

        // Assert — every wired Generated/Proactor directory contains the full canonical suite (AC-13, FR-14)
        var missing = new List<string>();

        foreach (var dir in _proactorGeneratedDirs)
        {
            foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    missing.Add($"  Proactor: {filePath}");
            }
        }

        Assert.True(missing.Count == 0,
            $"Canonical Proactor files absent after regeneration — run ./generate-test.sh and rebuild:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void When_generating_everywhere_all_canonical_reactor_tests_should_match_ledger_skip()
    {
        AssertCanonicalSkipMatchesLedger(_reactorGeneratedDirs, "Reactor");
    }

    [Fact]
    public void When_generating_everywhere_all_canonical_proactor_tests_should_match_ledger_skip()
    {
        AssertCanonicalSkipMatchesLedger(_proactorGeneratedDirs, "Proactor");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A canonical generated test must carry the ledger-driven Deferred Skip IFF its ledger cell
    /// is not yet Pass/Fixed. The Skip marker may be #NNNN (Unknown) or a real #&lt;n&gt; (Deferred).
    /// The expectation is computed from the same conformance ledger the generator reads, so this
    /// gate stays correct as the rollout proves behaviours (ADR 0067 "How master stays green").
    /// </summary>
    private static void AssertCanonicalSkipMatchesLedger(
        IReadOnlyList<string> variantDirs, string variant)
    {
        var ledger = LoadRealLedger();
        var violations = new List<string>();

        foreach (var dir in variantDirs)
        {
            var ledgerKey = LedgerKeyForGeneratedDir(dir);

            foreach (var (templateName, frColumn) in TEMPLATE_FR_COLUMNS)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    continue; // absence already caught by the presence test above

                // GetSkip returns a non-empty Deferred string unless the cell is Pass/Fixed.
                var expectSkip = ledger.GetSkip(ledgerKey, frColumn, templateName).Length > 0;
                var hasSkip = File.ReadAllText(filePath).Contains("Skip = \"Deferred:");

                if (expectSkip && !hasSkip)
                    violations.Add(
                        $"  {variant} missing Skip (ledger '{ledgerKey}' / {frColumn} is not Pass/Fixed): {filePath}");
                else if (!expectSkip && hasSkip)
                    violations.Add(
                        $"  {variant} unexpected Skip (ledger '{ledgerKey}' / {frColumn} is Pass/Fixed): {filePath}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Canonical {variant} tests' Skip state disagrees with the conformance ledger:\n" +
            string.Join("\n", violations));
    }

    private static Paramore.Brighter.Test.Generator.ConformanceLedger LoadRealLedger()
    {
        var ledgerPath =
            Paramore.Brighter.Test.Generator.ConformanceLedger.FindLedgerPath(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate the conformance ledger from AppContext.BaseDirectory.");
        return new Paramore.Brighter.Test.Generator.ConformanceLedger(ledgerPath);
    }

    /// <summary>
    /// Resolves the conformance-ledger row key for a Generated/{variant} directory by reading the
    /// owning project's test-configuration.json — the same source of truth the generator uses.
    /// Handles both multi-configuration ("MessagingGateways": { "&lt;Config&gt;": … }) and
    /// single-configuration ("MessagingGateway": { … }) project shapes.
    /// </summary>
    private static string LedgerKeyForGeneratedDir(string variantDir)
    {
        // variantDir = <project>/MessagingGateway[/<Config>]/Generated/<variant>
        var generatedDir = Directory.GetParent(variantDir)!.FullName;        // …/Generated
        var configOrGatewayDir = Directory.GetParent(generatedDir)!.FullName; // …/<Config> or …/MessagingGateway
        var configName = Path.GetFileName(configOrGatewayDir);

        var isSingleConfig = string.Equals(configName, "MessagingGateway", StringComparison.Ordinal);

        var projectDir = FindProjectDir(configOrGatewayDir)
            ?? throw new InvalidOperationException(
                $"Could not find test-configuration.json above {variantDir}");

        using var json = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectDir, "test-configuration.json")));
        var gateway = isSingleConfig
            ? json.RootElement.GetProperty("MessagingGateway")
            : json.RootElement.GetProperty("MessagingGateways").GetProperty(configName);

        return gateway.GetProperty("LedgerKey").GetString()
            ?? throw new InvalidOperationException(
                $"test-configuration.json for {projectDir} ({configName}) has no LedgerKey.");
    }

    private static string? FindProjectDir(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "test-configuration.json")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds a directory containing
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c>, which is a reliable marker for the repo root.
    /// Returns null when no such directory is found.
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

    /// <summary>
    /// Returns all <c>Generated/{variant}</c> directories (Reactor or Proactor) found
    /// recursively under <paramref name="testsRoot"/>. The canonical pattern is
    /// <c>tests/Paramore.Brighter.*.Tests/MessagingGateway/**/Generated/{variant}</c>.
    /// </summary>
    private static IReadOnlyList<string> FindGeneratedVariantDirs(string testsRoot, string variant)
    {
        var results = new List<string>();

        foreach (var testProject in Directory.EnumerateDirectories(testsRoot, "Paramore.Brighter.*.Tests"))
        {
            var gatewayRoot = Path.Combine(testProject, "MessagingGateway");
            if (!Directory.Exists(gatewayRoot))
                continue;

            foreach (var generatedDir in Directory.EnumerateDirectories(
                         gatewayRoot, "Generated", SearchOption.AllDirectories))
            {
                var variantDir = Path.Combine(generatedDir, variant);
                if (Directory.Exists(variantDir))
                    results.Add(variantDir);
            }
        }

        return results.OrderBy(d => d).ToList();
    }
}
