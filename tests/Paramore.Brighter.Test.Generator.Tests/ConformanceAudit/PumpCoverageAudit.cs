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
/// A pump behaviour the ledger depends on that no test in the tree covers.
/// </summary>
/// <param name="Kind">The violation category. Always <c>PumpBehaviourNotCovered</c>.</param>
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
/// Matching is by file name. That catches the risk actually being guarded against — a test being
/// deleted or moved out of the tree — and costs nothing at scan time. It does not read test bodies,
/// so it cannot notice a test that survives in name while being hollowed out; that is a code-review
/// concern rather than an audit one.
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
    /// Scans the pump test tree under <paramref name="repoRoot"/> and reports every required
    /// behaviour left without a covering test. Makes no network calls and spawns no subprocess.
    /// </summary>
    /// <param name="repoRoot">The repository root holding <c>tests/</c>.</param>
    /// <returns>The aggregate result; <see cref="PumpCoverageResult.Violations"/> is empty when intact.</returns>
    public static PumpCoverageResult CheckCoverage(string repoRoot)
    {
        var dispatchDir = Path.Combine([repoRoot, .. s_dispatchPathSegments]);
        var testFileNames = EnumerateTestFileNames(dispatchDir);

        var violations = RequiredBehaviours
            .Where(behaviour => !testFileNames.Any(name => Covers(name, behaviour)))
            .Select(behaviour => new PumpCoverageViolation(
                "PumpBehaviourNotCovered",
                behaviour.Description,
                $"no file under {Path.Combine(s_dispatchPathSegments)} has all of "
                + $"[{string.Join(", ", behaviour.FileNameFragments)}] in its name; "
                + $"expected something like {behaviour.ExampleTestName}.cs"))
            .ToList();

        return new PumpCoverageResult(violations);
    }

    /// <summary>
    /// True when every fragment the behaviour requires appears in <paramref name="testFileName"/>.
    /// </summary>
    private static bool Covers(string testFileName, RequiredPumpBehaviour behaviour) =>
        behaviour.FileNameFragments.All(fragment =>
            testFileName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every <c>.cs</c> file name beneath <paramref name="dispatchDir"/>, or empty when the tree is
    /// absent — a missing tree is itself total loss of coverage, and is reported as such rather than
    /// throwing.
    /// </summary>
    private static IReadOnlyList<string> EnumerateTestFileNames(string dispatchDir) =>
        Directory.Exists(dispatchDir)
            ? Directory.EnumerateFiles(dispatchDir, "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToList()
            : [];
}
