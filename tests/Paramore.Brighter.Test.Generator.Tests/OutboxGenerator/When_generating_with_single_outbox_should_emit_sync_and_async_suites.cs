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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class SingleOutboxSuiteGenerationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public SingleOutboxSuiteGenerationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Fact]
    public async Task When_generating_with_single_outbox_should_emit_sync_and_async_suites()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",

            // The singular form, with no Prefix - as Paramore.Brighter.DynamoDB.Tests,
            // Paramore.Brighter.DynamoDB.V4.Tests and Paramore.Brighter.MongoDb.Tests declare it.
            Outbox = new OutboxConfiguration
            {
                Transaction = "SqlTransaction",
                OutboxProvider = "MsSqlOutbox",
                SupportsTransactions = true,
            },
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        var generated = Path.Combine(_testDirectory, "Outbox", "Generated");

        Assert.True(
            File.Exists(Path.Combine(generated, "Sync", "IAmAnOutboxProviderSync.cs")),
            "a singular Outbox should generate the Sync suite"
        );
        Assert.True(
            File.Exists(
                Path.Combine(
                    generated,
                    "Sync",
                    "When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties.cs"
                )
            ),
            "the generated Sync suite should include the outbox conformance tests"
        );
        Assert.True(
            File.Exists(Path.Combine(generated, "Async", "IAmAnOutboxProviderAsync.cs")),
            "a singular Outbox should still generate the Async suite"
        );
        Assert.True(
            File.Exists(Path.Combine(generated, "Causation", "CausationTrackingOutboxTests.cs")),
            "a singular Outbox should still generate the Causation suite"
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
