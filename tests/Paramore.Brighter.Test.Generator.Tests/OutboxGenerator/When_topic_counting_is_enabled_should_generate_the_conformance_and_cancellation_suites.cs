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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class TopicCountingSuiteGenerationTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), $"TopicCountingGeneratorTests_{Guid.NewGuid():N}");

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, null)]
    [Arguments(true, null)]
    public async Task When_topic_counting_is_enabled_should_generate_the_conformance_and_cancellation_suites(
        bool multipleOutboxes, bool? supportsTopicCounting)
    {
        //Arrange
        Directory.CreateDirectory(_testDirectory);
        var outbox = new Dictionary<string, object>
        {
            ["Transaction"] = "System.Data.Common.DbTransaction",
            ["OutboxProvider"] = "TestOutboxProvider",
            ["CollectionName"] = "TopicCounts",
            ["Category"] = "TestDatabase"
        };
        if (supportsTopicCounting.HasValue)
            outbox["SupportsTopicCounting"] = supportsTopicCounting.Value;
        var root = new Dictionary<string, object> { ["Namespace"] = "MyApp.Tests" };
        if (multipleOutboxes)
        {
            root["Outboxes"] = new Dictionary<string, object>
            {
                ["Enabled"] = outbox,
                ["Default"] = new { Transaction = "System.Data.Common.DbTransaction", OutboxProvider = "DefaultOutboxProvider" }
            };
        }
        else
        {
            root["Outbox"] = outbox;
        }
        var configPath = Path.Combine(_testDirectory, "test-configuration.json");
        await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(root));
        var configuration = TestConfigurationLoader.Load(configPath, _testDirectory)!;
        var generator = new Generators.OutboxGenerator(NullLogger<Generators.OutboxGenerator>.Instance);
        var suiteDirectory = Path.Combine(_testDirectory, "Outbox", multipleOutboxes ? "Enabled" : "", "Generated", "TopicCounting");

        //Act
        var planned = generator.Plan(configuration);
        await generator.GenerateAsync(configuration);

        //Assert
        var plannedSuite = planned.Where(file => Path.GetDirectoryName(file.DestinationPath) == suiteDirectory)
            .Select(file => file.DestinationPath).OrderBy(path => path).ToArray();
        if (supportsTopicCounting == true)
        {
            await Assert.That(plannedSuite).IsNotEmpty();
            await Assert.That(Directory.Exists(suiteDirectory)).IsTrue();
            var generated = Directory.GetFiles(suiteDirectory, "*.cs").OrderBy(path => path).ToArray();
            await Assert.That(generated).IsEqualTo(plannedSuite);
            var sources = generated.Select(File.ReadAllText).ToArray();
            await Assert.That(System.Linq.Enumerable.Any(sources, source => source.Contains("TopicCountingOutboxBaseTests<System.Data.Common.DbTransaction>"))).IsTrue();
            await Assert.That(System.Linq.Enumerable.Any(sources, source => source.Contains("When_a_topic_count_is_cancelled_should_end_the_call"))).IsTrue();
            var tests = generated.Where(path => !Path.GetFileName(path).StartsWith("IAm", StringComparison.Ordinal));
            foreach (var path in tests)
{
    var source = File.ReadAllText(path);
    await Assert.That(source).Contains("[NotInParallel(\"TopicCounts\")]");
    await Assert.That(source).Contains("[Property(\"Category\", \"TestDatabase\")]");
    await Assert.That(source).DoesNotContain("Skip =");
}
        }
        else
        {
            await Assert.That(plannedSuite).IsEmpty();
            await Assert.That(Directory.Exists(suiteDirectory)).IsFalse();
        }
        if (multipleOutboxes)
        {
            var defaultSuite = Path.Combine(_testDirectory, "Outbox", "Default", "Generated", "TopicCounting");
            await Assert.That(Directory.Exists(defaultSuite)).IsFalse();
            await Assert.That(System.Linq.Enumerable.Any(planned, file => Path.GetDirectoryName(file.DestinationPath) == defaultSuite)).IsFalse();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }
}
