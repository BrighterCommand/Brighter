using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Structural integration gate for FR-13 (AC-13), FR-14, and FR-21 (ADR 0067 stage (i)).
///
/// After a full regeneration with `./generate-test.sh`, every wired gateway configuration's
/// Generated/Reactor and Generated/Proactor directories must contain the complete canonical
/// suite. Because every conformance-ledger cell is currently Unknown, every canonical test
/// must carry a Deferred: #NNNN Skip.
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

    // The exact count of wired gateway configurations declared across the nine wired
    // test projects (FR-13). This is a regression guard: a new wiring changes the count.
    private const int EXPECTED_WIRED_CONFIGURATION_COUNT = 20;

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
    public void When_generating_everywhere_all_canonical_reactor_tests_should_carry_deferred_skip()
    {
        // Arrange — resolved in constructor; all ledger cells are Unknown so every canonical
        // test must carry "Deferred: #NNNN" (ADR 0067 "How master stays green").

        // Assert — every canonical Reactor file carries the ledger-driven Skip
        var violations = new List<string>();

        foreach (var dir in _reactorGeneratedDirs)
        {
            foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    continue; // absence already caught by the presence test above

                var content = File.ReadAllText(filePath);
                if (!content.Contains("Skip = \"Deferred: #NNNN"))
                    violations.Add($"  Reactor missing Skip: {filePath}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Canonical Reactor tests missing Deferred Skip (all ledger cells are Unknown):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void When_generating_everywhere_all_canonical_proactor_tests_should_carry_deferred_skip()
    {
        // Arrange — resolved in constructor; all ledger cells are Unknown so every canonical
        // test must carry "Deferred: #NNNN" (ADR 0067 "How master stays green").

        // Assert — every canonical Proactor file carries the ledger-driven Skip
        var violations = new List<string>();

        foreach (var dir in _proactorGeneratedDirs)
        {
            foreach (var templateName in CANONICAL_TEMPLATE_NAMES)
            {
                var filePath = Path.Combine(dir, $"{templateName}.cs");
                if (!File.Exists(filePath))
                    continue; // absence already caught by the presence test above

                var content = File.ReadAllText(filePath);
                if (!content.Contains("Skip = \"Deferred: #NNNN"))
                    violations.Add($"  Proactor missing Skip: {filePath}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Canonical Proactor tests missing Deferred Skip (all ledger cells are Unknown):\n" +
            string.Join("\n", violations));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
