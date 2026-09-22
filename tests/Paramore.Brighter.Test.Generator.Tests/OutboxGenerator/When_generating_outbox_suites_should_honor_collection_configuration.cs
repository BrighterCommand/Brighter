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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class OutboxCollectionGenerationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Theory]
    [InlineData("Sync", false, "SharedOutbox")]
    [InlineData("Async", false, "SharedOutbox")]
    [InlineData("Causation", false, "SharedOutbox")]
    [InlineData("Sync", true, "SharedOutbox")]
    [InlineData("Async", true, "SharedOutbox")]
    [InlineData("Causation", true, "SharedOutbox")]
    [InlineData("Sync", false, null)]
    [InlineData("Async", false, null)]
    [InlineData("Causation", false, null)]
    [InlineData("Sync", true, null)]
    [InlineData("Async", true, null)]
    [InlineData("Causation", true, null)]
    public async Task When_generating_outbox_suites_should_honor_collection_configuration(
        string suite, bool usePluralConfiguration, string? collectionName)
    {
        // Arrange
        var outbox = new OutboxConfiguration
        {
            Transaction = "System.Data.Common.DbTransaction",
            OutboxProvider = "TestOutboxProvider",
            CollectionName = collectionName,
            SupportsTransactions = true
        };
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder"
        };
        if (usePluralConfiguration)
        {
            configuration.Outboxes = new Dictionary<string, OutboxConfiguration>
            {
                ["Text"] = outbox,
                ["Binary"] = new OutboxConfiguration
                {
                    Transaction = "System.Data.Common.DbTransaction",
                    OutboxProvider = "BinaryOutboxProvider",
                    CollectionName = "BinaryOutbox"
                }
            };
        }
        else
        {
            configuration.Outbox = outbox;
        }

        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var suiteDirectory = Path.Combine(_testDirectory, "Outbox",
            usePluralConfiguration ? "Text" : string.Empty, "Generated", suite);
        AssertCollection(suiteDirectory, collectionName);
        if (usePluralConfiguration)
        {
            AssertCollection(Path.Combine(_testDirectory, "Outbox", "Binary", "Generated", suite), "BinaryOutbox");
        }
    }

    private static void AssertCollection(string suiteDirectory, string? collectionName)
    {
        var testFiles = Directory.GetFiles(suiteDirectory, "*.cs")
            .Where(file => !Path.GetFileName(file).StartsWith("IAmAnOutboxProvider", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(testFiles);
        Assert.All(testFiles, file =>
        {
            var source = File.ReadAllText(file);
            if (collectionName == null)
            {
                Assert.DoesNotContain("[Collection(", source);
            }
            else
            {
                Assert.Contains($"[Collection(\"{collectionName}\")]", source);
                Assert.Single(source.Split('\n'),
                    line => line.TrimStart().StartsWith("[Collection(", StringComparison.Ordinal));
            }
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
