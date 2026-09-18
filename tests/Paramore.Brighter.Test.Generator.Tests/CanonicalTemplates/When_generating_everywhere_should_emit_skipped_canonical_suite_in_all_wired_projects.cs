using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Paramore.Brighter.Test.Generator;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Structural integration gate for the generate-everywhere rule (ADR 0067 stage (i)).
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
/// Reference: ADR 0067 "generate everywhere immediately", stage (i).
/// </summary>
public class GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
{
    // The canonical behaviours, taken from the generator's own map rather than copied. A local copy
    // drifted from it once already: it stopped at eleven entries while the generator emitted twelve,
    // so the behaviour it omitted was generated into every wired configuration and checked in none.
    // Reading the generator's map is what makes "the full canonical suite" mean the same thing here
    // as it does in the generator.
    private static IReadOnlyDictionary<string, string> TEMPLATE_FR_COLUMNS
        => CanonicalBehaviours.TEMPLATE_FR_COLUMNS;

    // The canonical file base names, which are the keys of that same map.
    private static IEnumerable<string> CANONICAL_TEMPLATE_NAMES => TEMPLATE_FR_COLUMNS.Keys;

    // The exact count of wired gateway configurations declared across the ten wired
    // test projects. This is a regression guard: a new wiring changes the count.
    private const int EXPECTED_WIRED_CONFIGURATION_COUNT = 24;

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

    /// <summary>
    /// Reports, for each supplied generated directory, the canonical files that are absent from it.
    /// This is the presence check both variant gates apply; it is exposed so that a canary test can
    /// establish that the check actually notices an absent canonical behaviour, which a gate that
    /// scans the real tree can never demonstrate while the tree is correct.
    /// </summary>
    /// <param name="generatedDirs">The Generated/Reactor or Generated/Proactor directories to scan.</param>
    /// <param name="variant">The variant name used to label each reported path.</param>
    /// <returns>One entry per absent canonical file, empty when every directory holds the full suite.</returns>
    public static IReadOnlyList<string> FindMissingCanonicalFiles(
        IEnumerable<string> generatedDirs,
        string variant)
    {
        var missing = new List<string>();

        foreach (var dir in generatedDirs)
        {
            foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    missing.Add($"  {variant}: {filePath}");
            }
        }

        return missing;
    }

    [Fact]
    public void When_generating_everywhere_should_find_exactly_twenty_wired_configurations()
    {
        // Arrange — resolved in constructor

        // Assert — both variants must agree on the configuration count.
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _reactorGeneratedDirs.Count);
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _proactorGeneratedDirs.Count);
    }

    [Fact]
    public void When_generating_everywhere_all_wired_reactor_directories_should_contain_canonical_suite()
    {
        // Arrange — resolved in constructor

        // Assert — every wired Generated/Reactor directory contains the full canonical suite
        var missing = FindMissingCanonicalFiles(_reactorGeneratedDirs, "Reactor");

        Assert.True(missing.Count == 0,
            $"Canonical Reactor files absent after regeneration — run ./generate-test.sh and rebuild:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void When_generating_everywhere_all_wired_proactor_directories_should_contain_canonical_suite()
    {
        // Arrange — resolved in constructor

        // Assert — every wired Generated/Proactor directory contains the full canonical suite
        var missing = FindMissingCanonicalFiles(_proactorGeneratedDirs, "Proactor");

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
