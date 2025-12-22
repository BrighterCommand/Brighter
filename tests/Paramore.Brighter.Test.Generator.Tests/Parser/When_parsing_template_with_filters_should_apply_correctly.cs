using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithFiltersShouldApplyCorrectly : IDisposable
{
    private readonly string _testDirectory;

    public WhenParsingTemplateWithFiltersShouldApplyCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }
    
    [Fact]
    public async Task When_parsing_template_with_filters_should_apply_correctly()
    {
        // Arrange
        const string template = "{{ Name | upcase }}";
        var templatePath = Path.Combine(_testDirectory, "filter.liquid");
        var outputPath = Path.Combine(_testDirectory, "filter_output.txt");
        await File.WriteAllTextAsync(templatePath, template);
        
        var model = new { Name = "test" };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(templatePath, outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(outputPath);
        Assert.Equal("TEST", result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
