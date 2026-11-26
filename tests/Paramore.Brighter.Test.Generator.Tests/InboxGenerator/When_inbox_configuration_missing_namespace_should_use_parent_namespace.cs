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

namespace Paramore.Brighter.Test.Generator.Tests.InboxGenerator;

public class WhenInboxConfigurationMissingNamespaceShouldUseParentNamespace : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.InboxGenerator> _logger;

    public WhenInboxConfigurationMissingNamespaceShouldUseParentNamespace()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"InboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.InboxGenerator>();
    }

    [Fact]
    public async Task When_inbox_configuration_missing_namespace_should_use_parent_namespace()
    {
        // Arrange
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

        // Assert - asserted through the generated file rather than through the configuration
        // object, because the generator no longer writes its per-render values back onto the
        // caller's configuration
        var generated = File.ReadAllText(Path.Combine(
            _testDirectory, "Inbox", "DynamoDB", "Generated", "Sync",
            "When_Adding_A_Command_To_The_Inbox_It_Can_Be_Retrieved.cs"));
        Assert.Contains("namespace MyApp.Tests.Inbox.DynamoDB.Sync;", generated);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
