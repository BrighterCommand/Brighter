using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithConditionalShouldRenderCorrectly : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _templatePath;
    private readonly string _outputPath;

    public WhenParsingTemplateWithConditionalShouldRenderCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _templatePath = Path.Combine(_testDirectory, "template.liquid");
        _outputPath = Path.Combine(_testDirectory, "output.txt");
    }

    [Fact]
    public async Task When_parsing_template_with_conditional_should_render_correctly()
    {
        // Arrange
        const string template = "{% if IsActive %}Active{% else %}Inactive{% endif %}";
        await File.WriteAllTextAsync(_templatePath, template);
        var model = new { IsActive = true };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(_templatePath, _outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(_outputPath);
        Assert.Equal("Active", result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
