using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.Parser;

public class WhenParsingComplexTemplateWithMultipleFeaturesShouldRenderCorrectly : IDisposable
{
    private readonly string _testDirectory;

    public WhenParsingComplexTemplateWithMultipleFeaturesShouldRenderCorrectly()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParserIntegrationTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task When_parsing_complex_template_with_multiple_features_should_render_correctly()
    {
        // Arrange
        const string template = @"
namespace {{ Namespace }}
{
    {% if HasSupportToTransaction %}
    public class {{ Prefix }}TransactionTests
    {
        {% for test in Tests %}
        [Fact]
        public void {{ test }}() { }
        {% endfor %}
    }
    {% endif %}
}";
        var templatePath = Path.Combine(_testDirectory, "complex.liquid");
        var outputPath = Path.Combine(_testDirectory, "complex_output.txt");
        await File.WriteAllTextAsync(templatePath, template);

        var model = new
        {
            Namespace = "MyApp.Tests",
            Prefix = "SqlServer",
            HasSupportToTransaction = true,
            Tests = new[] { "Test1", "Test2" }
        };

        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(templatePath, outputPath, model);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("namespace MyApp.Tests", result);
        Assert.Contains("SqlServerTransactionTests", result);
        Assert.Contains("Test1", result);
        Assert.Contains("Test2", result);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
