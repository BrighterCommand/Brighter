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

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class OutstandingCountSuiteGenerationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");

    [Test]
    public async Task When_generating_outbox_suites_should_render_the_outstanding_count_suite_for_every_outbox()
    {
        // Arrange
        var configuration = Configure(supportsTransactions: false, category: null);
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var templates = await TemplateNames();
        await Assert.That(templates).Contains("IAmAnOutstandingCountOutboxProvider.cs");
        await Assert.That(templates.Length).IsEqualTo(4);
        foreach (var folder in new[] { "Text", "Binary" })
        {
            var rendered = Directory.GetFiles(SuiteDirectory(folder), "*.cs").Select(Path.GetFileName).ToArray();
            await Assert.That(rendered.OrderBy(name => name)).IsEqualTo(templates.OrderBy(name => name));
        }
    }

    [Test]
    public async Task When_planning_outbox_suites_should_list_the_outstanding_count_suite_for_every_outbox()
    {
        // Arrange
        var configuration = Configure(supportsTransactions: false, category: null);
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);

        // Act
        var planned = generator.Plan(configuration);

        // Assert
        foreach (var folder in new[] { "Text", "Binary" })
        {
            var expected = (await TemplateNames()).Select(name => Path.Combine(SuiteDirectory(folder), name));
            var listed = planned.Select(file => file.DestinationPath)
                .Where(path => string.Equals(Path.GetDirectoryName(path), SuiteDirectory(folder), StringComparison.Ordinal));
            await Assert.That(listed.OrderBy(path => path)).IsEqualTo(expected.OrderBy(path => path));
        }
    }

    [Test]
    [Arguments("InMemoryCategory", true)]
    [Arguments(null, false)]
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
            await Assert.That(testFiles).IsNotEmpty();
            foreach (var file in testFiles)
{
    var source = File.ReadAllText(file);
    if (expectTrait)
        await Assert.That(source).Contains($"[Property(\"Category\", \"{category}\")]");
    else
        await Assert.That(source).DoesNotContain("[Property(");
}
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

    private static async Task<string[]> TemplateNames()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Templates", "Outbox", "OutstandingCount");
        await Assert.That(Directory.Exists(folder)).IsTrue().Because($"The suite's template folder {folder} does not exist");
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
