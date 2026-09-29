using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Test.Generator.Configuration;
using Paramore.Brighter.Test.Generator.Tests.GeneratedFileAudit;


namespace Paramore.Brighter.Test.Generator.Tests.GatewayConformanceTemplate;

public class WhenRenderingTheGatewayConformanceTemplateWithGenericSubjectsShouldRenderOpenGenericLiterals : IDisposable
{
    private readonly string _outputDirectory;
    private readonly string _outputPath;

    public WhenRenderingTheGatewayConformanceTemplateWithGenericSubjectsShouldRenderOpenGenericLiterals()
    {
        _outputDirectory = Path.Combine(Path.GetTempPath(), $"GatewayConformanceTemplateTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_outputDirectory);
        _outputPath = Path.Combine(_outputDirectory, "output.cs");
    }

    [Test]
    public async Task When_rendering_the_gateway_conformance_template_with_generic_subjects_should_render_open_generic_literals()
    {
        // Arrange
        // Evident Data: two generic AdditionalExpectedSubjects, written with the arity backtick
        // exactly as Type.FullName reports it, alongside a non-generic SubscriptionType - the
        // union both sides of which the rendered expected set must carry.
        var repositoryRoot = Directory.GetParent(GeneratedTreeAudit.LocateTestsRoot())!.FullName;
        var templatePath = Path.Combine(
            repositoryRoot, "tools", "Paramore.Brighter.Test.Generator", "Templates", "GatewayConformance",
            "When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs.liquid");
        var configuration = new GatewayConformanceConfiguration
        {
            Namespace = "MyApp.Tests",
            SubscriptionType = "Ns.Root",
            AdditionalExpectedSubjects = ["Ns.Foo`1", "Ns.Bar`2"]
        };
        var parser = new Generator.Parser();
        var context = new Generator.ParseContext(templatePath, _outputPath, configuration);

        // Act
        await parser.ParseAsync(context);

        // Assert
        var result = await File.ReadAllTextAsync(_outputPath);
        await Assert.That(result).Contains("typeof(Ns.Root)");
        await Assert.That(result).Contains("typeof(Ns.Foo<>)");
        await Assert.That(result).Contains("typeof(Ns.Bar<,>)");
        await Assert.That(result).DoesNotContain("`");
        await Assert.That(result).Contains("namespace MyApp.Tests.MessagingGateway.Generated.Conformance");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, true);
        }
    }
}
