using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithOutboxConfigurationShouldRenderCorrectly : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _templatePath;
    private readonly string _outputPath;

    public WhenParsingTemplateWithOutboxConfigurationShouldRenderCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _templatePath = Path.Combine(_testDirectory, "template.liquid");
        _outputPath = Path.Combine(_testDirectory, "output.txt");
    }
    
    [Test]
    public async Task When_parsing_template_with_outbox_configuration_should_render_correctly()
    {
        // Arrange
        const string template = "namespace {{ Namespace }}\npublic class {{ Prefix }}Tests { }";
        await File.WriteAllTextAsync(_templatePath, template);
        var outboxConfig = new OutboxConfiguration { Namespace = "MyApp.Tests", Prefix = "SqlServer" };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(_templatePath, _outputPath, outboxConfig);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(_outputPath);
        await Assert.That(result).Contains("namespace MyApp.Tests");
        await Assert.That(result).Contains("public class SqlServerTests");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
