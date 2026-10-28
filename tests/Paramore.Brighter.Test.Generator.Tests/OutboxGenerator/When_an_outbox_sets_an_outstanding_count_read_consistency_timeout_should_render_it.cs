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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class OutstandingCountReadConsistencyTimeoutGenerationTests : IDisposable
{
    private const string AgeTest =
        "When_counting_outstanding_messages_older_than_the_age_should_count_only_stale_ones.cs";

    private const string CeilingTest =
        "When_counting_outstanding_messages_with_a_ceiling_should_stop_at_the_ceiling.cs";

    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Theory]
    [InlineData(AgeTest)]
    [InlineData(CeilingTest)]
    public async Task When_an_outbox_sets_an_outstanding_count_read_consistency_timeout_should_render_it(string testFile)
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountReadConsistencyTimeoutInMilliseconds = 10000;

        // Act
        await Generate(outbox);

        // Assert
        var source = File.ReadAllText(Path.Combine(SuiteDirectory(), testFile));
        Assert.Contains(
            "protected override System.TimeSpan ReadConsistencyTimeout => System.TimeSpan.FromMilliseconds(10000);",
            source);
    }

    [Theory]
    [InlineData(AgeTest)]
    [InlineData(CeilingTest)]
    public async Task When_an_outbox_leaves_the_outstanding_count_read_consistency_timeout_unset_should_not_override_it(
        string testFile)
    {
        // Arrange
        var outbox = Outbox();

        // Act
        await Generate(outbox);

        // Assert
        var source = File.ReadAllText(Path.Combine(SuiteDirectory(), testFile));
        Assert.DoesNotContain("ReadConsistencyTimeout", source);
    }

    [Theory]
    [InlineData(AgeTest)]
    [InlineData(CeilingTest)]
    public async Task When_an_outbox_sets_only_the_causation_read_consistency_timeout_should_not_render_it_for_the_outstanding_count(
        string testFile)
    {
        // Arrange
        var outbox = Outbox();
        outbox.ReadConsistencyTimeoutInMilliseconds = 7000;

        // Act
        await Generate(outbox);

        // Assert
        var source = File.ReadAllText(Path.Combine(SuiteDirectory(), testFile));
        Assert.DoesNotContain("ReadConsistencyTimeout", source);
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
