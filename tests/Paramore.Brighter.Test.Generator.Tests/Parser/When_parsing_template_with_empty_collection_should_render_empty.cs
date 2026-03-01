using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithEmptyCollectionShouldRenderEmpty : IDisposable
{
    private readonly string _testDirectory;

    public WhenParsingTemplateWithEmptyCollectionShouldRenderEmpty()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task When_parsing_template_with_empty_collection_should_render_empty()
    {
        // Arrange
        const string template = "{% for item in Items %}{{ item }}{% endfor %}";
        var templatePath = Path.Combine(_testDirectory, "empty_collection.liquid");
        var outputPath = Path.Combine(_testDirectory, "empty_collection_output.txt");
        await File.WriteAllTextAsync(templatePath, template);

        var model = new { Items = Array.Empty<string>() };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(templatePath, outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(outputPath);
        Assert.Empty(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
