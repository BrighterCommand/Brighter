using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.CanonicalTemplates;

/// <summary>
/// Canary for the generate-everywhere presence gate. A gate scanning a complete tree is green whether or not it
/// looks for a behaviour, so these facts hand the presence check a directory missing one file of the canonical
/// set (the generator's <see cref="Paramore.Brighter.Test.Generator.CanonicalBehaviours.TEMPLATE_FR_COLUMNS"/>)
/// and require it to say so; and a complete directory, which it must not report.
/// </summary>
public class CanonicalBehaviourPresenceCanaryTests
{
    // The canonical behaviour most recently added to the generator's set.
    private const string REQUEUE_TOO_MANY_TIMES =
        "When_requeuing_a_message_too_many_times_should_move_to_dead_letter_queue";

    // The control: a behaviour the gate has always known about.
    private const string PLAIN_REQUEUE =
        "When_requeuing_a_failed_message_should_be_redelivered";

    [Fact]
    public void When_the_requeue_too_many_times_file_is_absent_should_be_reported_missing()
    {
        // Arrange
        using var generated = new GeneratedDirectoryOmitting(REQUEUE_TOO_MANY_TIMES);

        // Act
        var missing = GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
            .FindMissingCanonicalFiles([generated.Path], "Reactor");

        // Assert
        Assert.Single(missing);
        Assert.Contains(REQUEUE_TOO_MANY_TIMES, missing[0]);
    }

    [Fact]
    public void When_the_plain_requeue_file_is_absent_should_be_reported_missing()
    {
        // Arrange
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
        // Arrange
        using var generated = new GeneratedDirectoryOmitting(omitted: null);

        // Act
        var missing = GeneratingEverywhereShouldEmitSkippedCanonicalSuiteTests
            .FindMissingCanonicalFiles([generated.Path], "Reactor");

        // Assert
        Assert.Empty(missing);
    }

    // The files are empty: only their names matter to a presence check.
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
