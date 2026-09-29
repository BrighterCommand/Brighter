using System;
using System.IO;
using System.Threading.Tasks;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithLoopShouldRenderCorrectly : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _templatePath;
    private readonly string _outputPath;

    public WhenParsingTemplateWithLoopShouldRenderCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _templatePath = Path.Combine(_testDirectory, "template.liquid");
        _outputPath = Path.Combine(_testDirectory, "output.txt");
    }

    [Test]
    public async Task When_parsing_template_with_loop_should_render_correctly()
    {
        // Arrange
        const string template = "{% for item in Items %}{{ item }}\n{% endfor %}";
        await File.WriteAllTextAsync(_templatePath, template);
        var model = new { Items = new[] { "Item1", "Item2", "Item3" } };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(_templatePath, _outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(_outputPath);
        await Assert.That(result).Contains("Item1");
        await Assert.That(result).Contains("Item2");
        await Assert.That(result).Contains("Item3");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
