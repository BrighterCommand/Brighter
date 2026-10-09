using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Canary for the generate-everywhere presence gate.
///
/// The gate that scans the real tree can only ever report what is absent from it, and the tree is
/// currently complete — so a green gate is equally consistent with "every canonical behaviour is
/// present" and with "the gate does not look for that behaviour at all". These facts settle which,
/// by handing the presence check a directory that is deliberately missing one canonical file and
/// requiring it to say so.
///
/// The canonical set is taken from the generator's own
/// <see cref="Paramore.Brighter.Test.Generator.CanonicalBehaviours.TEMPLATE_FR_COLUMNS"/>, which is
/// what "canonical" means: a behaviour the generator emits for every wired configuration. A gate
/// that checks a subset of it is blind in exactly the part it omits.
/// </summary>
public class CanonicalBehaviourPresenceCanaryTests
{
    // Judged against the requeue-too-many-times behaviour (FR-23 in the conformance ledger), the
    // canonical behaviour most recently added to the generator's set.
    private const string REQUEUE_TOO_MANY_TIMES =
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue";

    // Judged against the plain requeue behaviour (FR-22 in the conformance ledger). Used as the
    // control: a behaviour the gate has always known about.
    private const string PLAIN_REQUEUE =
        "When_requeuing_a_failed_message_should_be_redelivered";

    [Fact]
    public void When_the_requeue_too_many_times_file_is_absent_should_be_reported_missing()
    {
        // Arrange — a generated directory holding every canonical file but that one
        using var generated = new GeneratedDirectoryOmitting(REQUEUE_TOO_MANY_TIMES);

        // Act
        var missing = GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
            .FindMissingCanonicalFiles([generated.Path], "Reactor");

        // Assert — the gate must notice the behaviour it was never told to look for
        Assert.Single(missing);
        Assert.Contains(REQUEUE_TOO_MANY_TIMES, missing[0]);
    }

    [Fact]
    public void When_the_plain_requeue_file_is_absent_should_be_reported_missing()
    {
        // Arrange — the control: a behaviour the gate has always checked
        using var generated = new GeneratedDirectoryOmitting(PLAIN_REQUEUE);

        // Act
        var missing = GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
            .FindMissingCanonicalFiles([generated.Path], "Reactor");

        // Assert
        Assert.Single(missing);
        Assert.Contains(PLAIN_REQUEUE, missing[0]);
    }

    [Fact]
    public void When_every_canonical_file_is_present_should_report_nothing_missing()
    {
        // Arrange — omit nothing
        using var generated = new GeneratedDirectoryOmitting(omitted: null);

        // Act
        var missing = GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
            .FindMissingCanonicalFiles([generated.Path], "Reactor");

        // Assert — a check that reports a complete directory as incomplete would make the other
        // two facts pass for the wrong reason
        Assert.Empty(missing);
    }

    /// <summary>
    /// A temporary Generated/Reactor-shaped directory holding one empty file per canonical
    /// behaviour, less the one named. Only the file names matter to a presence check.
    /// </summary>
    private sealed class GeneratedDirectoryOmitting : System.IDisposable
    {
        public string Path { get; }

        public GeneratedDirectoryOmitting(string? omitted)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "canonical-presence-canary-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);

            var canonical = Paramore.Brighter.Test.Generator.CanonicalBehaviours
                .TEMPLATE_FR_COLUMNS.Keys
                .Where(name => name != omitted);

            foreach (var name in canonical)
                File.WriteAllText(System.IO.Path.Combine(Path, $"{name}.cs"), string.Empty);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
