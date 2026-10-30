#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

public class OutstandingCountSuiteGenerationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Fact]
    public async Task When_generating_outbox_suites_should_render_the_outstanding_count_suite_for_every_outbox()
    {
        // Arrange
        var configuration = Configure(supportsTransactions: false, category: null);
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var templates = TemplateNames();
        Assert.Contains("IAmAnOutstandingCountOutboxProvider.cs", templates);
        Assert.Equal(4, templates.Length);
        foreach (var folder in new[] { "Text", "Binary" })
        {
            var rendered = Directory.GetFiles(SuiteDirectory(folder), "*.cs").Select(Path.GetFileName).ToArray();
            Assert.Equal(templates.OrderBy(name => name), rendered.OrderBy(name => name));
        }
    }

    [Fact]
    public void When_planning_outbox_suites_should_list_the_outstanding_count_suite_for_every_outbox()
    {
        // Arrange
        var configuration = Configure(supportsTransactions: false, category: null);
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        var planned = generator.Plan(configuration);

        // Assert
        foreach (var folder in new[] { "Text", "Binary" })
        {
            var expected = TemplateNames().Select(name => Path.Combine(SuiteDirectory(folder), name));
            var listed = planned.Select(file => file.DestinationPath)
                .Where(path => string.Equals(Path.GetDirectoryName(path), SuiteDirectory(folder), StringComparison.Ordinal));
            Assert.Equal(expected.OrderBy(path => path), listed.OrderBy(path => path));
        }
    }

    [Theory]
    [InlineData("InMemoryCategory", true)]
    [InlineData(null, false)]
    public async Task When_generating_outbox_suites_should_apply_the_category_to_every_outstanding_count_test_file(
        string? category, bool expectTrait)
    {
        // Arrange
        var configuration = Configure(supportsTransactions: true, category);
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        foreach (var folder in new[] { "Text", "Binary" })
        {
            var testFiles = Directory.GetFiles(SuiteDirectory(folder), "*.cs")
                .Where(file => !Path.GetFileName(file).StartsWith("IAmAnOutstandingCountOutboxProvider", StringComparison.Ordinal))
                .ToArray();
            Assert.NotEmpty(testFiles);
            Assert.All(testFiles, file =>
            {
                var source = File.ReadAllText(file);
                if (expectTrait)
                    Assert.Contains($"[Trait(\"Category\", \"{category}\")]", source);
                else
                    Assert.DoesNotContain("[Trait(", source);
            });
        }
    }

    private TestConfiguration Configure(bool supportsTransactions, string? category)
    {
        return new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            Outboxes = new Dictionary<string, OutboxConfiguration>
            {
                ["Text"] = new()
                {
                    Transaction = "System.Data.Common.DbTransaction",
                    OutboxProvider = "TextOutboxProvider",
                    SupportsTransactions = supportsTransactions,
                    Category = category
                },
                ["Binary"] = new()
                {
                    Transaction = "System.Data.Common.DbTransaction",
                    OutboxProvider = "BinaryOutboxProvider",
                    SupportsTransactions = supportsTransactions,
                    Category = category
                }
            }
        };
    }

    private string SuiteDirectory(string folder) =>
        Path.Combine(_testDirectory, "Outbox", folder, "Generated", "OutstandingCount");

    private static string[] TemplateNames()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Templates", "Outbox", "OutstandingCount");
        Assert.True(Directory.Exists(folder), $"The suite's template folder {folder} does not exist");
        return Directory.GetFiles(folder, "*.liquid")
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
