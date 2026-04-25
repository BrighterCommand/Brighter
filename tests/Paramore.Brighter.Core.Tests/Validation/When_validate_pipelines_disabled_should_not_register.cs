#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesDisabledTests
{
    private static IBrighterBuilder CreateBuilder(out ServiceCollection services)
    {
        services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        return new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
    }

    [Fact]
    public void When_validate_pipelines_disabled_should_not_register_validator()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act
        var returnedBuilder = builder.ValidatePipelines(enabled: false);

        // Assert — no validator, no hosted service registered
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IAmAPipelineValidator));
        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(IHostedService)
            && sd.ImplementationType?.Name == "BrighterValidationHostedService");
        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void When_validate_pipelines_enabled_should_register_validator()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act
        builder.ValidatePipelines(enabled: true);

        // Assert — validator and hosted service registered (same as default)
        Assert.Contains(services, sd => sd.ServiceType == typeof(IAmAPipelineValidator));
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IHostedService)
            && sd.ImplementationType?.Name == "BrighterValidationHostedService");
    }

    [Fact]
    public void When_validate_pipelines_default_should_register_validator()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act — no arguments, default should be enabled
        builder.ValidatePipelines();

        // Assert
        Assert.Contains(services, sd => sd.ServiceType == typeof(IAmAPipelineValidator));
    }

    [Fact]
    public void When_describe_pipelines_disabled_should_not_register_writer()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act
        var returnedBuilder = builder.DescribePipelines(enabled: false);

        // Assert — no diagnostic writer, no hosted service registered
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IAmAPipelineDiagnosticWriter));
        Assert.DoesNotContain(services, sd =>
            sd.ServiceType == typeof(IHostedService)
            && sd.ImplementationType?.Name == "BrighterDiagnosticHostedService");
        Assert.Same(builder, returnedBuilder);
    }

    [Fact]
    public void When_describe_pipelines_enabled_should_register_writer()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act
        builder.DescribePipelines(enabled: true);

        // Assert — diagnostic writer and hosted service registered (same as default)
        Assert.Contains(services, sd => sd.ServiceType == typeof(IAmAPipelineDiagnosticWriter));
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IHostedService)
            && sd.ImplementationType?.Name == "BrighterDiagnosticHostedService");
    }

    [Fact]
    public void When_describe_pipelines_default_should_register_writer()
    {
        // Arrange
        var builder = CreateBuilder(out var services);

        // Act — no arguments, default should be enabled
        builder.DescribePipelines();

        // Assert
        Assert.Contains(services, sd => sd.ServiceType == typeof(IAmAPipelineDiagnosticWriter));
    }
}
