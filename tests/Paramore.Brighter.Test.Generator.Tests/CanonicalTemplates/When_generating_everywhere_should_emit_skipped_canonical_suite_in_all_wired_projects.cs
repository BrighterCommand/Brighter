using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Paramore.Brighter.Test.Generator;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// After a full regeneration, every wired configuration's Generated/Reactor and Generated/Proactor directories
/// hold the whole canonical suite, and each canonical test carries the Deferred Skip exactly when its ledger
/// cell is not yet Pass or Fixed. The expectation is read from the same ledger the generator reads. Whether
/// the generated tests compile or pass is for the solution build and the brokers. See ADR 0067, How master
/// stays green.
/// </summary>
public class GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
{
    // Taken from the generator's own map, so "the full canonical suite" means the same here as there.
    private static IReadOnlyDictionary<string, string> TEMPLATE_FR_COLUMNS
        => CanonicalBehaviours.TEMPLATE_FR_COLUMNS;

    private static IEnumerable<string> CANONICAL_TEMPLATE_NAMES => TEMPLATE_FR_COLUMNS.Keys;

    // A regression guard: wiring a new configuration changes it.
    private const int EXPECTED_WIRED_CONFIGURATION_COUNT = 27;

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
    public void When_generating_everywhere_should_find_all_wired_configurations()
    {
        // Assert
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _reactorGeneratedDirs.Count);
        Assert.Equal(EXPECTED_WIRED_CONFIGURATION_COUNT, _proactorGeneratedDirs.Count);
    }

    [Fact]
    public void When_generating_everywhere_all_wired_reactor_directories_should_contain_canonical_suite()
    {
        // Assert
        var missing = FindMissingCanonicalFiles(_reactorGeneratedDirs, "Reactor");

        Assert.True(missing.Count == 0,
            $"Canonical Reactor files absent after regeneration — run ./generate-test.sh and rebuild:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void When_generating_everywhere_all_wired_proactor_directories_should_contain_canonical_suite()
    {
        // Assert
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
                    continue;

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

    // variantDir is <project>/MessagingGateway[/<Config>]/Generated/<variant>; read the row key from the
    // owning project's test-configuration.json, in its single- or multi-configuration shape.
    private static string LedgerKeyForGeneratedDir(string variantDir)
    {
        var generatedDir = Directory.GetParent(variantDir)!.FullName;
        var configOrGatewayDir = Directory.GetParent(generatedDir)!.FullName;
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
