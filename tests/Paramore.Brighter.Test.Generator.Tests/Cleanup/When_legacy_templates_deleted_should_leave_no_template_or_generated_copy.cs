using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Cleanup;

/// <summary>
/// Gate test for the Phase 5 step-C cleanup (ADR 0066 "Step C").
///
/// Asserts:
///   1. None of the four legacy template filenames exists under
///      tools/.../Templates/MessagingGateway/{Reactor,Proactor}/ (FR-10(3), FR-12, FR-19).
///   2. No generated copy of any of the four remains under any
///      tests/Paramore.Brighter.*.Tests/**/Generated/ directory (AC-10(b), AC-12, AC-22).
///   3. No messaging-gateway template that purports to exercise delayed requeue calls
///      Requeue/RequeueAsync without a non-null TimeSpan (AC-12).
/// </summary>
public class WhenLegacyTemplatesDeletedShouldLeaveNoTemplateOrGeneratedCopy
{
    // The four legacy gated template base names — exactly this closed list (ADR 0066 "Step C").
    // IMPORTANT: match these exactly. The substring-matching hazard (ADR 0066) means
    // a glob like *with_delay* would also match the canonical FR-2 template
    // (When_requeuing_a_failed_message_with_delay_should_redeliver_after_delay).
    private static readonly string[] LEGACY_TEMPLATE_NAMES =
    [
        "When_reading_a_delayed_message_via_the_messaging_gateway_should_delay_delivery",
        "When_requeuing_a_failed_message_should_receive_message_again",
        "When_requeuing_a_failed_message_with_delay_should_receive_message_again",
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue",
    ];

    // Substrings in a template name that indicate it purports to exercise delayed requeue.
    // Templates with these substrings must pass a non-null TimeSpan to Requeue/RequeueAsync (AC-12).
    // Note: the canonical FR-22 and FR-15 templates legitimately call Requeue without a positive delay
    // (plain requeue and zero-delay, respectively), and neither contains the word "delay" in its name.
    // This check is scoped to templates whose name implies delayed requeue behavior.
    private static readonly string[] DELAYED_REQUEUE_NAME_INDICATORS =
    [
        "with_delay",      // implies a delayed requeue is exercised
        "delayed_message", // implies a delayed message send is exercised
    ];

    private readonly string _repoRoot;
    private readonly string _templateRoot;
    private readonly string _testsRoot;

    public WhenLegacyTemplatesDeletedShouldLeaveNoTemplateOrGeneratedCopy()
    {
        _repoRoot = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from AppContext.BaseDirectory. " +
                "Expected to walk up and find 'tests/Paramore.Brighter.Kafka.Tests'.");

        _templateRoot = Path.Combine(
            _repoRoot, "tools", "Paramore.Brighter.Test.Generator",
            "Templates", "MessagingGateway");

        _testsRoot = Path.Combine(_repoRoot, "tests");
    }

    [Fact]
    public void When_legacy_templates_deleted_should_not_find_legacy_template_files_under_reactor_or_proactor()
    {
        // Arrange
        var reactorDir = Path.Combine(_templateRoot, "Reactor");
        var proactorDir = Path.Combine(_templateRoot, "Proactor");
        var found = new System.Collections.Generic.List<string>();

        // Act
        foreach (var name in LEGACY_TEMPLATE_NAMES)
        {
            var reactorPath = Path.Combine(reactorDir, $"{name}.cs.liquid");
            var proactorPath = Path.Combine(proactorDir, $"{name}.cs.liquid");

            if (File.Exists(reactorPath))
                found.Add(reactorPath);
            if (File.Exists(proactorPath))
                found.Add(proactorPath);
        }

        // Assert — no legacy template files remain (FR-10(3), FR-12, FR-19)
        Assert.True(found.Count == 0,
            $"Legacy template files still present — delete them (ADR 0066 Step C):\n" +
            string.Join("\n", found.Select(f => $"  {f}")));
    }

    [Fact]
    public void When_legacy_templates_deleted_should_not_find_generated_copies_under_any_test_project()
    {
        // Arrange
        var found = new System.Collections.Generic.List<string>();

        // Act — scan every Generated/ directory under every test project, at any depth,
        // for the legacy file names (the requirement is not scoped to MessagingGateway/
        // or to the Reactor/Proactor variant sub-directories)
        foreach (var testProject in Directory.EnumerateDirectories(_testsRoot, "Paramore.Brighter.*.Tests"))
        {
            foreach (var generatedDir in Directory.EnumerateDirectories(
                         testProject, "Generated", SearchOption.AllDirectories))
            {
                foreach (var name in LEGACY_TEMPLATE_NAMES)
                {
                    found.AddRange(Directory.EnumerateFiles(
                        generatedDir, $"{name}.cs", SearchOption.AllDirectories));
                }
            }
        }

        // Assert — no generated copies remain (AC-10(b), AC-12, AC-22)
        Assert.True(found.Count == 0,
            $"Generated copies of legacy templates still present — manually delete them " +
            $"(ADR 0066 Step C; the generator never deletes stale files):\n" +
            string.Join("\n", found.Select(f => $"  {f}")));
    }

    [Fact]
    public void When_legacy_templates_deleted_every_delayed_requeue_template_should_pass_non_null_timespan()
    {
        // Arrange — collect all remaining .liquid templates under MessagingGateway/
        var violations = new System.Collections.Generic.List<string>();
        var allTemplates = Directory.EnumerateFiles(
            _templateRoot, "*.cs.liquid", SearchOption.AllDirectories).ToList();

        // Act — check templates whose name implies delayed requeue exercise
        foreach (var templatePath in allTemplates)
        {
            var baseName = Path.GetFileNameWithoutExtension(
                Path.GetFileNameWithoutExtension(templatePath)); // strip .liquid then .cs

            var isDelayedRequeue = DELAYED_REQUEUE_NAME_INDICATORS
                .Any(indicator => baseName.Contains(indicator, System.StringComparison.OrdinalIgnoreCase));

            if (!isDelayedRequeue)
                continue; // not a delayed-requeue template; plain requeue (FR-22) and zero-delay (FR-15) are fine

            var content = File.ReadAllText(templatePath);

            // The template must call Requeue/RequeueAsync with a non-null TimeSpan argument.
            // A call like _channel.Requeue(msg) or channel.RequeueAsync(msg) with no TimeSpan
            // argument is the defect FR-12 addressed. We accept TimeSpan.FromSeconds(...)
            // and TimeSpan.Zero as non-null, but a bare Requeue(msg) / Requeue(msg, null)
            // with no TimeSpan argument is a violation.
            var hasRequeue = content.Contains("Requeue(") || content.Contains("RequeueAsync(");
            if (!hasRequeue)
                continue; // not a Requeue call site; e.g. a SendWithDelay-only template

            // A conforming delayed-requeue template must contain "TimeSpan" in the Requeue call.
            // We check the file-level content rather than parsing individual call sites —
            // if the file calls Requeue/RequeueAsync and contains TimeSpan, it is conforming.
            if (!content.Contains("TimeSpan"))
            {
                violations.Add(
                    $"  {templatePath}\n    → calls Requeue/RequeueAsync without a non-null TimeSpan (AC-12)");
            }
        }

        // Assert — every delayed-requeue template passes a TimeSpan (AC-12, FR-12)
        Assert.True(violations.Count == 0,
            $"Messaging-gateway template(s) that purport to exercise delayed requeue call " +
            $"Requeue/RequeueAsync without a non-null TimeSpan:\n" +
            string.Join("\n", violations));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks up from <paramref name="startDir"/> until it finds a directory containing
    /// <c>tests/Paramore.Brighter.Kafka.Tests</c>, which is a reliable marker for the repo root.
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
}
