using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// A pump behaviour the conformance ledger leans on, and the test that covers it.
/// </summary>
/// <param name="Description">
/// The behaviour as the ledger's "division of labour" section names it.
/// </param>
/// <param name="FileNameFragments">
/// Fragments that must all appear in a covering test's file name. Held as fragments rather than a
/// whole name so that a test may be renamed around them without a false alarm.
/// </param>
/// <param name="ExampleTestName">
/// A real file name that satisfies <paramref name="FileNameFragments"/>, quoted in the violation so
/// a reader can see what the audit is looking for.
/// </param>
public sealed record RequiredPumpBehaviour(
    string Description,
    IReadOnlyList<string> FileNameFragments,
    string ExampleTestName);

/// <summary>
/// A pump behaviour the ledger depends on that the tree no longer covers as claimed.
/// </summary>
/// <param name="Kind">
/// The violation category. <c>PumpBehaviourNotCovered</c> when no pump variant covers the behaviour
/// at all; <c>PumpBehaviourVariantNotCovered</c> when one variant covers it and another does not,
/// which is the FR-14 parity failure.
/// </param>
/// <param name="Behaviour">The <see cref="RequiredPumpBehaviour.Description"/> left uncovered.</param>
/// <param name="Detail">What the audit looked for, and where.</param>
public sealed record PumpCoverageViolation(string Kind, string Behaviour, string Detail);

/// <summary>
/// The aggregate result of a pump-coverage scan.
/// </summary>
/// <param name="Violations">Behaviours with no covering test. Empty when the tree is intact.</param>
public sealed record PumpCoverageResult(IReadOnlyList<PumpCoverageViolation> Violations);

/// <summary>
/// Audits that the pump-side half of the conformance claim still exists in the tree.
/// </summary>
/// <remarks>
/// <para>
/// The conformance ledger is valid compositionally. Its gateway suite proves that a transport
/// honours a requeue or a reject, and deliberately does not drive a pump; the pump's half — deciding
/// *when* to requeue or reject — is proven against in-memory channels in
/// <c>tests/Paramore.Brighter.Core.Tests/MessageDispatch</c>.
/// </para>
/// <para>
/// Nothing else links the two halves. Delete those pump tests and every ledger cell stays green
/// while the composite claim stops being true, because the gateway suite never asserted the pump's
/// half in the first place. This audit supplies the link, and fails loudly when a behaviour the
/// ledger leans on loses its coverage.
/// </para>
/// <para>
/// Coverage is required in <em>both</em> pump variants. FR-14 makes Reactor/Proactor parity the
/// standard the ledger's cells are read against, and the <c>MessageDispatch</c> tree carries a
/// <c>Reactor</c> and a <c>Proactor</c> directory for exactly that reason. A behaviour surviving in
/// one of them proves half of what the ledger claims, so it is reported rather than accepted.
/// </para>
/// <para>
/// A behaviour is located by file name and then confirmed by content. The name catches a test
/// deleted or moved out of the tree; the content catches the same loss wearing the name of the test
/// it replaced, because a covering test stripped of its assertions keeps passing while proving
/// nothing. The content bar is only that an assertion survives — whether it is a <em>good</em>
/// assertion remains a code-review concern rather than an audit one.
/// </para>
/// </remarks>
public static class PumpCoverageAudit
{
    /// <summary>
    /// The pump behaviours the conformance ledger depends on, as recorded in the "division of
    /// labour" section of <c>conformance-status.md</c>. Adding a row here without a covering test
    /// fails the audit, which is the intended direction of travel.
    /// </summary>
    public static IReadOnlyList<RequiredPumpBehaviour> RequiredBehaviours { get; } =
    [
        new("a deferring handler is requeued until rejected",
            ["defer_message", "requeued_until_rejected"],
            "When_a_command_handler_throws_a_defer_message_Then_message_is_requeued_until_rejected"),

        new("requeue count threshold reached",
            ["requeue_count_threshold"],
            "When_a_requeue_count_threshold_for_commands_has_been_reached"),

        new("channel failure exception retries until reconnected",
            ["channel_failure_exception"],
            "When_a_channel_failure_exception_is_thrown_for_event_should_retry_until_connection_re_established"),

        new("the unacceptable message limit",
            ["unacceptable_message_limit"],
            "When_an_unacceptable_message_limit_is_reached"),

        new("reject falls back to the DLQ with no invalid channel",
            ["no_imq_configured_reject_falls_back"],
            "When_no_imq_configured_reject_falls_back_to_dlq")
    ];

    /// <summary>
    /// The tree the pump half lives in, relative to the repository root.
    /// </summary>
    private static readonly string[] s_dispatchPathSegments =
        ["tests", "Paramore.Brighter.Core.Tests", "MessageDispatch"];

    /// <summary>
    /// The pump variants FR-14 requires parity across, named as their directories under
    /// <see cref="s_dispatchPathSegments"/>.
    /// </summary>
    private static readonly string[] s_pumpVariants = ["Reactor", "Proactor"];

    /// <summary>
    /// Scans the pump test tree under <paramref name="repoRoot"/> and reports every required
    /// behaviour left without a covering test in both pump variants. Makes no network calls and
    /// spawns no subprocess.
    /// </summary>
    /// <param name="repoRoot">The repository root holding <c>tests/</c>.</param>
    /// <returns>The aggregate result; <see cref="PumpCoverageResult.Violations"/> is empty when intact.</returns>
    public static PumpCoverageResult CheckCoverage(string repoRoot)
    {
        var dispatchDir = Path.Combine([repoRoot, .. s_dispatchPathSegments]);
        var filesByVariant = s_pumpVariants.ToDictionary(
            variant => variant,
            variant => EnumerateTestFiles(Path.Combine(dispatchDir, variant)));

        var violations = RequiredBehaviours
            .SelectMany(behaviour => ViolationsFor(behaviour, filesByVariant))
            .ToList();

        return new PumpCoverageResult(violations);
    }

    /// <summary>
    /// Reports what <paramref name="behaviour"/> has lost, in the terms whoever reads the failure
    /// needs to act: <c>PumpBehaviourNotCovered</c> when no variant names it at all (a deletion),
    /// <c>PumpBehaviourVariantNotCovered</c> when one pump kept it and another dropped it (an FR-14
    /// parity break), and <c>PumpBehaviourCoverageHollowedOut</c> when a file still bears the name
    /// but no longer asserts (go and read that file).
    /// </summary>
    private static IEnumerable<PumpCoverageViolation> ViolationsFor(
        RequiredPumpBehaviour behaviour,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filesByVariant)
    {
        var namedIn = s_pumpVariants
            .ToDictionary(
                variant => variant,
                variant => filesByVariant[variant]
                    .Where(file => Covers(Path.GetFileNameWithoutExtension(file), behaviour))
                    .ToList());

        var missingFrom = s_pumpVariants.Where(variant => namedIn[variant].Count == 0).ToList();
        var hollowIn = s_pumpVariants
            .Where(variant => namedIn[variant].Count > 0 && !namedIn[variant].Any(Asserts))
            .ToList();

        var fragments = string.Join(", ", behaviour.FileNameFragments);

        if (missingFrom.Count == s_pumpVariants.Length)
        {
            return
            [
                new PumpCoverageViolation(
                    "PumpBehaviourNotCovered",
                    behaviour.Description,
                    $"no file under {Path.Combine(s_dispatchPathSegments)} has all of "
                    + $"[{fragments}] in its name; "
                    + $"expected something like {behaviour.ExampleTestName}.cs")
            ];
        }

        var parityBreaks = missingFrom.Select(variant => new PumpCoverageViolation(
            "PumpBehaviourVariantNotCovered",
            behaviour.Description,
            $"covered in {string.Join(" and ", s_pumpVariants.Except(missingFrom))} but not in "
            + $"{variant}: no file under "
            + $"{Path.Combine([.. s_dispatchPathSegments, variant])} has all of "
            + $"[{fragments}] in its name. FR-14 requires both pump variants."));

        var hollowed = hollowIn.Select(variant => new PumpCoverageViolation(
            "PumpBehaviourCoverageHollowedOut",
            behaviour.Description,
            $"{variant} still names this behaviour but no longer asserts it — "
            + $"{string.Join(", ", namedIn[variant].Select(Path.GetFileName))} "
            + "contains no assertion. A test that asserts nothing passes while proving nothing, "
            + "so the ledger cells leaning on it are no longer earned."));

        return parityBreaks.Concat(hollowed);
    }

    /// <summary>
    /// True when every fragment the behaviour requires appears in <paramref name="testFileName"/>.
    /// </summary>
    private static bool Covers(string testFileName, RequiredPumpBehaviour behaviour) =>
        behaviour.FileNameFragments.All(fragment =>
            testFileName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when <paramref name="testFile"/> still makes at least one assertion.
    /// </summary>
    /// <remarks>
    /// The lowest bar that separates a test from an empty method, and deliberately so: the audit
    /// cannot judge whether an assertion is a <em>good</em> one, which stays a code-review concern.
    /// What it can do is refuse to count a body with nothing in it. xUnit's <c>Assert</c> is the
    /// only assertion form this repository uses.
    /// </remarks>
    private static bool Asserts(string testFile) =>
        File.ReadAllText(testFile).Contains("Assert.", StringComparison.Ordinal);

    /// <summary>
    /// Every <c>.cs</c> file beneath <paramref name="variantDir"/>, or empty when the tree is
    /// absent — a missing tree is itself total loss of coverage, and is reported as such rather than
    /// throwing.
    /// </summary>
    private static IReadOnlyList<string> EnumerateTestFiles(string variantDir) =>
        Directory.Exists(variantDir)
            ? Directory.EnumerateFiles(variantDir, "*.cs", SearchOption.AllDirectories).ToList()
            : [];
}
