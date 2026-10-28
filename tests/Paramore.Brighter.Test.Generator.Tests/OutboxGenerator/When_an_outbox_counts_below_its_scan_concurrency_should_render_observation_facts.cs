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

public class OutstandingCountBelowScanConcurrencyGenerationTests : IDisposable
{
    private const string CeilingTest =
        "When_counting_outstanding_messages_with_a_ceiling_should_stop_at_the_ceiling.cs";

    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task When_an_outbox_counts_below_its_scan_concurrency_should_render_observation_facts(int ceiling)
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountBelowScanConcurrency = true;

        // Act
        var source = await GenerateCeilingTest(outbox);

        // Assert
        Assert.Contains($"_output.WriteLine(await ObserveCountAsync({ceiling}));", source);
        Assert.Contains("ITestOutputHelper", source);
    }

    [Fact]
    public async Task When_an_outbox_counts_below_its_scan_concurrency_should_assert_nothing_in_the_observation_facts()
    {
        // Arrange
        var outbox = Outbox();
        outbox.OutstandingCountBelowScanConcurrency = true;

        // Act
        var source = await GenerateCeilingTest(outbox);

        // Assert
        var observation = source.Substring(source.IndexOf("ObserveCountAsync(1)", StringComparison.Ordinal));
        Assert.DoesNotContain("Assert.", observation);
    }

    [Fact]
    public async Task When_an_outbox_leaves_the_scan_concurrency_setting_unset_should_not_render_observation_facts()
    {
        // Arrange
        var outbox = Outbox();

        // Act
        var source = await GenerateCeilingTest(outbox);

        // Assert
        Assert.DoesNotContain("ObserveCountAsync", source);
        Assert.DoesNotContain("ITestOutputHelper", source);
    }

    private static OutboxConfiguration Outbox() => new()
    {
        Transaction = "System.Data.Common.DbTransaction",
        OutboxProvider = "TestOutboxProvider",
        SupportsTransactions = false
    };

    private async Task<string> GenerateCeilingTest(OutboxConfiguration outbox)
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

        return File.ReadAllText(Path.Combine(_testDirectory, "Outbox", "Generated", "OutstandingCount", CeilingTest));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
