#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class OutstandingCountGapGenerationTests : IDisposable
{
    private const string AgeTest =
        "When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones.cs";

    private const string CeilingTest =
        "When_counting_outstanding_messages_with_a_ceiling_should_stop_at_the_ceiling.cs";

    private const string CancellationTest =
        "When_an_outstanding_count_is_cancelled_should_end_the_call.cs";

    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Fact]
    public async Task When_the_age_column_is_a_gap_should_render_a_deferred_skip_on_the_age_fact()
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountAgeGap = 1234;

        // Act
        await Generate(outbox);

        // Assert
        AssertSkipped(AgeTest, "When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones", 1234);
        AssertNotSkipped(CeilingTest);
        AssertNotSkipped(CancellationTest);
    }

    [Fact]
    public async Task When_the_ceiling_column_is_a_gap_should_render_a_deferred_skip_on_both_ceiling_facts()
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountCeilingGap = 1235;

        // Act
        await Generate(outbox);

        // Assert
        AssertSkipped(CeilingTest, "When_counting_outstanding_messages_with_a_ceiling_of_three_should_return_three", 1235);
        AssertSkipped(CeilingTest, "When_counting_outstanding_messages_with_a_ceiling_of_five_should_return_five", 1235);
        AssertNotSkipped(AgeTest);
        AssertNotSkipped(CancellationTest);
    }

    [Fact]
    public async Task When_the_cancellation_column_is_a_gap_should_render_a_deferred_skip_on_the_cancellation_fact()
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountCancellationGap = 1236;

        // Act
        await Generate(outbox);

        // Assert
        AssertSkipped(CancellationTest, "When_an_outstanding_count_is_cancelled_should_end_the_call", 1236);
        AssertNotSkipped(AgeTest);
        AssertNotSkipped(CeilingTest);
    }

    [Fact]
    public async Task When_no_outstanding_count_gap_is_marked_should_not_render_a_skip()
    {
        // Arrange
        var outbox = Outbox();

        // Act
        await Generate(outbox);

        // Assert
        AssertNotSkipped(AgeTest);
        AssertNotSkipped(CeilingTest);
        AssertNotSkipped(CancellationTest);
    }

    [Fact]
    public async Task When_the_ceiling_column_is_a_gap_should_skip_the_observation_facts_too()
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountCeilingGap = 1235;
        outbox.OutstandingCountBelowScanConcurrency = true;

        // Act
        await Generate(outbox);

        // Assert
        AssertSkipped(CeilingTest, "When_counting_outstanding_messages_with_a_ceiling_of_one_should_report_the_count", 1235);
        AssertSkipped(CeilingTest, "When_counting_outstanding_messages_with_a_ceiling_of_two_should_report_the_count", 1235);
    }

    private void AssertSkipped(string testFile, string method, int issue)
    {
        var source = File.ReadAllText(Path.Combine(SuiteDirectory(), testFile));
        var pattern = $"\\[Fact\\(Skip = \"Deferred: #{issue} [^\"]+\"\\)\\]\\s+public async Task {method}\\(";
        Assert.Matches(new Regex(pattern), source);
    }

    private void AssertNotSkipped(string testFile)
    {
        var source = File.ReadAllText(Path.Combine(SuiteDirectory(), testFile));
        Assert.DoesNotContain("Skip =", source);
    }

    private static OutboxConfiguration Outbox() => new()
    {
        Transaction = "System.Data.Common.DbTransaction",
        OutboxProvider = "TestOutboxProvider",
        SupportsTransactions = false
    };

    private async Task Generate(OutboxConfiguration outbox)
    {
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            Outbox = outbox
        };
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        await generator.GenerateAsync(configuration);
    }

    private string SuiteDirectory() =>
        Path.Combine(_testDirectory, "Outbox", "Generated", "OutstandingCount");

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
