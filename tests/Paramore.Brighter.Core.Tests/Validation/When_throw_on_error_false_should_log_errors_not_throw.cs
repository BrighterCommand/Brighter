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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ThrowOnErrorFalseTests
{
    private static BrighterValidationHostedService BuildService(
        BrighterPipelineValidationOptions options,
        IAmAPipelineValidator validator,
        SpyLogger<BrighterValidationHostedService> logger)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        return new BrighterValidationHostedService(
            Options.Create(options),
            validator,
            provider,
            logger);
    }

    [Fact]
    public void When_validate_pipelines_with_throw_on_error_false_should_store_in_options()
    {
        // Arrange
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        // Act
        builder.ValidatePipelines(throwOnError: false);

        // Assert — ThrowOnError should be false in the resolved options
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>().Value;
        Assert.False(options.ThrowOnError);
    }

    [Fact]
    public void When_validate_pipelines_with_throw_on_error_true_should_store_in_options()
    {
        // Arrange
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        // Act
        builder.ValidatePipelines(throwOnError: true);

        // Assert — ThrowOnError should be true (the default)
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>().Value;
        Assert.True(options.ThrowOnError);
    }

    [Fact]
    public void When_validate_pipelines_default_should_have_throw_on_error_true()
    {
        // Arrange
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        // Act — no throwOnError argument
        builder.ValidatePipelines();

        // Assert — default ThrowOnError should be true
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>().Value;
        Assert.True(options.ThrowOnError);
    }

    [Fact]
    public async Task When_throw_on_error_false_and_errors_should_log_not_throw()
    {
        // Arrange
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = SpyPipelineValidator.WithErrors(error);
        var logger = new SpyLogger<BrighterValidationHostedService>();
        var service = BuildService(
            new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false, ThrowOnError = false },
            validator,
            logger);

        // Act — should NOT throw
        await service.StartAsync(CancellationToken.None);

        // Assert — error should be logged, not thrown
        Assert.True(validator.ValidateWasCalled);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("misconfigured"));
    }

    [Fact]
    public async Task When_throw_on_error_true_and_errors_should_throw()
    {
        // Arrange
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = SpyPipelineValidator.WithErrors(error);
        var logger = new SpyLogger<BrighterValidationHostedService>();
        var service = BuildService(
            new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false, ThrowOnError = true },
            validator,
            logger);

        // Act & Assert — should throw PipelineValidationException
        await Assert.ThrowsAsync<PipelineValidationException>(
            () => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task When_throw_on_error_false_should_still_log_warnings()
    {
        // Arrange
        var warning = new ValidationError(ValidationSeverity.Warning, "TestHandler", "Backstop ordering suboptimal");
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = new SpyPipelineValidator(new PipelineValidationResult([error], [warning]));
        var logger = new SpyLogger<BrighterValidationHostedService>();
        var service = BuildService(
            new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false, ThrowOnError = false },
            validator,
            logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — both error and warning should be logged
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("misconfigured"));
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning && e.Message.Contains("suboptimal"));
    }
}
