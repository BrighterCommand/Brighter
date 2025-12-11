using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingSimpleTemplateShouldRenderCorrectly : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _templatePath;
    private readonly string _outputPath;

    public WhenParsingSimpleTemplateShouldRenderCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _templatePath = Path.Combine(_testDirectory, "template.liquid");
        _outputPath = Path.Combine(_testDirectory, "output.txt");
    }

    [Fact]
    public async Task When_parsing_simple_template_should_render_correctly()
    {
        // Arrange
        const string template = "Hello {{ Name }}!";
        await File.WriteAllTextAsync(_templatePath, template);
        var model = new { Name = "World" };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(_templatePath, _outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(_outputPath);
        Assert.Equal("Hello World!", result);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
