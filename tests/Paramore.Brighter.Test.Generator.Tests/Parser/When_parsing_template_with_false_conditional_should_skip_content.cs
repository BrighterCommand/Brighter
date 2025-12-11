using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingTemplateWithFalseConditionalShouldSkipContent : IDisposable
{
    private readonly string _testDirectory;

    public WhenParsingTemplateWithFalseConditionalShouldSkipContent()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task When_parsing_template_with_false_conditional_should_skip_content()
    {
        // Arrange
        const string template = "{% if IsEnabled %}Enabled Content{% endif %}";
        var templatePath = Path.Combine(_testDirectory, "false_conditional.liquid");
        var outputPath = Path.Combine(_testDirectory, "false_conditional_output.txt");
        await File.WriteAllTextAsync(templatePath, template);
        
        var model = new { IsEnabled = false };
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
