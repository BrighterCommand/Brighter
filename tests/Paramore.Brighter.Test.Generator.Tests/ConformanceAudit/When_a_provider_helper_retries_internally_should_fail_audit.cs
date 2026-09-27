#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.ConformanceAudit;

/// <summary>
/// Canary for <see cref="DeadLetterPollContractAudit"/>, run against a synthetic tree.
/// </summary>
/// <remarks>
/// <para>
/// This pair guards the audit itself rather than the repository: one test proves the scan reports a
/// helper that retries internally, the other proves it stays quiet on a helper that does not. Both
/// halves are needed. A scan that reported everything would pass the first test while being
/// worthless, and a scan that reported nothing would pass the second.
/// </para>
/// <para>
/// The repository-facing test is <see cref="RealTreePollContractTests"/>.
/// </para>
/// </remarks>
public class DeadLetterPollContractAuditTests : IDisposable
{
    private readonly string _testDirectory;

    public DeadLetterPollContractAuditTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"PollContractTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void When_a_provider_helper_retries_internally_should_fail_audit()
    {
        // Arrange — a helper shaped the way every provider in the tree was shaped before this
        // contract existed: its own bounded loop, with a sleep between attempts
        WriteProvider("Pollster", """
            public class PollsterMessageGatewayProvider
            {
                public Message GetMessageFromDeadLetterQueue(Subscription subscription)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        var messages = _consumer.Receive(TimeSpan.FromSeconds(5));
                        var message = messages.First();
                        if (message.Header.MessageType != MessageType.MT_NONE)
                        {
                            return message;
                        }
                        Thread.Sleep(1000);
                    }

                    return new Message();
                }
            }
            """);

        // Act
        var result = DeadLetterPollContractAudit.Audit(_testDirectory);

        // Assert — both faults are named, so the report says what to remove
        Assert.Equal(1, result.HelpersScanned);
        Assert.Contains(result.Violations, v => v.Kind == "InternalRetryLoop");
        Assert.Contains(result.Violations, v => v.Kind == "InternalPollBackoff");
        Assert.All(result.Violations, v => Assert.Equal("GetMessageFromDeadLetterQueue", v.Helper));
    }

    [Fact]
    public void When_a_provider_helper_makes_a_single_bounded_receive_should_pass_audit()
    {
        // Arrange — the shape the contract asks for: one bounded receive, no loop, no sleep.
        // The `while` and the `Task.Delay` in the doc comment are the point of this fixture: a scan
        // that read comments as code would report this compliant helper.
        WriteProvider("SingleShot", """
            public class SingleShotMessageGatewayProvider
            {
                /// <summary>One bounded receive; the caller's while loop owns the retry and the
                /// Task.Delay between attempts.</summary>
                public async Task<Message> GetMessageFromInvalidChannelAsync(Subscription subscription)
                {
                    var messages = await _consumer.ReceiveAsync(TimeSpan.FromSeconds(5));
                    var message = messages.First();
                    if (message.Header.MessageType != MessageType.MT_NONE)
                    {
                        await _consumer.AcknowledgeAsync(message);
                    }

                    return message;
                }
            }
            """);

        // Act
        var result = DeadLetterPollContractAudit.Audit(_testDirectory);

        // Assert — scanned, and cleared
        Assert.Equal(1, result.HelpersScanned);
        Assert.True(result.Violations.Count == 0,
            "a single bounded receive is the shape the contract asks for, but the audit reported: "
            + string.Join("; ", result.Violations.Select(v => $"{v.Kind} — {v.Detail}")));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a provider into the synthetic tree at the path the audit walks.
    /// </summary>
    private void WriteProvider(string name, string source)
    {
        var directory = Path.Combine(_testDirectory, "tests", $"Fake.{name}.Tests", "MessagingGateway");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}MessageGatewayProvider.cs"), source);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
