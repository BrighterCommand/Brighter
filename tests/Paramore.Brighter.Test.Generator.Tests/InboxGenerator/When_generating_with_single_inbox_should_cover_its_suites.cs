#region Licence

/* The MIT License (MIT)
Copyright © 2026 Gilmar Filho <gilmarfilho75@gmail.com>

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

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.InboxGenerator;

public class SingleInboxSuiteGenerationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.InboxGenerator> _logger;

    public SingleInboxSuiteGenerationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.InboxGenerator>();
    }

    [Fact]
    public async Task When_generating_with_single_inbox_should_emit_sync_and_async_suites()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,

            // The singular form, with a Prefix - as Paramore.Brighter.DynamoDB.Tests and
            // Paramore.Brighter.MongoDb.Tests declare it. A singular inbox without a prefix is
            // skipped: there is no key to fall back on and the templates need the dot-qualified
            // suffix, so there is nothing sensible to generate into.
            Inbox = new InboxConfiguration
            {
                Prefix = "DynamoDB",
                InboxProvider = "TestInboxSync",
                InboxProviderAsync = "TestInboxAsync",
            },
        };
        var generator = new Generators.InboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var generated = Path.Combine(_testDirectory, "Inbox", "DynamoDB", "Generated");

        Assert.True(
            File.Exists(Path.Combine(generated, "Sync", "IAmAnInboxProviderSync.cs")),
            "a singular Inbox should generate the Sync suite"
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    generated,
                    "Sync",
                    "When_Adding_A_Command_To_The_Inbox_It_Can_Be_Retrieved.cs"
                )
            ),
            "the generated Sync suite should include the inbox conformance tests"
        );
        Assert.True(
            File.Exists(Path.Combine(generated, "Async", "IAmAnInboxProviderAsync.cs")),
            "a singular Inbox should still generate the Async suite"
        );
    }

    [Fact]
    public async Task When_generating_with_sync_provider_only_should_not_emit_async_suite()
    {
        // Arrange — the Async suite is conditional on InboxProviderAsync being set, so a sync-only
        // inbox is the case where generating it would be the failure
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,

            Inbox = new InboxConfiguration
            {
                Prefix = "DynamoDB",
                InboxProvider = "TestInboxSync",
            },
        };
        var generator = new Generators.InboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var generated = Path.Combine(_testDirectory, "Inbox", "DynamoDB", "Generated");

        Assert.True(
            File.Exists(Path.Combine(generated, "Sync", "IAmAnInboxProviderSync.cs")),
            "a sync-only Inbox should still generate the Sync suite"
        );
        Assert.False(
            Directory.Exists(Path.Combine(generated, "Async")),
            "a sync-only Inbox should not generate the Async suite"
        );
    }

    [Fact]
    public async Task When_planning_an_inbox_should_match_what_generation_writes()
    {
        // Arrange — Plan exists so the generated-tree audit can ask the generator what it owns;
        // its answer is only trustworthy if it is the same description generation walks
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,

            Inbox = new InboxConfiguration
            {
                Prefix = "DynamoDB",
                InboxProvider = "TestInboxSync",
                InboxProviderAsync = "TestInboxAsync",
            },
        };

        // Act
        var planned = new Generators.InboxGenerator(
                NullLogger<Generators.InboxGenerator>.Instance)
            .Plan(configuration)
            .Select(file => Path.GetFullPath(file.DestinationPath))
            .ToArray();

        await new Generators.InboxGenerator(_logger).GenerateAsync(configuration);

        var onDisk = Directory
            .EnumerateFiles(Path.Combine(_testDirectory, "Inbox"), "*", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToArray();
        // Assert — and non-vacuously: a plan of nothing would satisfy equality trivially
        Assert.NotEmpty(planned);
        Assert.Equal(planned.OrderBy(path => path), onDisk.OrderBy(path => path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
