#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Paramore.Brighter.Test.Generator.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.GatewayConformanceTemplate;

public class GatewayConformanceSubjectSnapshotTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"GatewayConformanceSnapshotTests_{Guid.NewGuid():N}");

    public GatewayConformanceSubjectSnapshotTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_generating_gateway_conformance_should_snapshot_additional_expected_subjects(
        bool hasAdditionalSubjects)
    {
        //Arrange
        var configurationPath = Path.Combine(_directory, TestConfigurationLoader.ConfigurationFileName);
        await File.WriteAllTextAsync(configurationPath, JsonSerializer.Serialize(new
        {
            Namespace = "Sample.Tests",
            GatewayConformance = new
            {
                SubscriptionType = "Sample.RootSubscription",
                AdditionalExpectedSubjects = hasAdditionalSubjects
                    ? new[] { "Sample.OriginalSubscription`1" }
                    : Array.Empty<string>()
            }
        }));
        var configuration = TestConfigurationLoader.Load(configurationPath, _directory)!;
        var subjects = configuration.GatewayConformance!.AdditionalExpectedSubjects;
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(
            new InMemoryGenerationLogProvider(() =>
            {
                subjects.Clear();
                subjects.Add("Sample.UpdatedSubscription");
            })));
        var generator = new Generators.GatewayConformanceGenerator(
            loggerFactory.CreateLogger<Generators.GatewayConformanceGenerator>());

        //Act
        await generator.GenerateAsync(configuration);

        //Assert
        var outputPath = Path.Combine(_directory, "MessagingGateway", "Generated", "Conformance",
            "When_sweeping_the_gateway_assembly_should_find_no_invalid_channel_factory_declaration.cs");
        var output = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("namespace Sample.Tests.MessagingGateway.Generated.Conformance;", output);
        Assert.Contains("typeof(Sample.RootSubscription)", output);
        Assert.DoesNotContain("Sample.UpdatedSubscription", output);
        if (hasAdditionalSubjects)
        {
            Assert.Contains("typeof(Sample.OriginalSubscription<>)", output);
        }
        Assert.Equal(new[] { "Sample.UpdatedSubscription" }, subjects);
        Assert.Same(subjects, configuration.GatewayConformance.AdditionalExpectedSubjects);
        Assert.Null(configuration.GatewayConformance.Namespace);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
