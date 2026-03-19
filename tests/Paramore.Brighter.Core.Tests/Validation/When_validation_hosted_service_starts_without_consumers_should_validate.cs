#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidationHostedServiceTests
{
    [Fact]
    public async Task When_consumer_does_not_own_validation_should_run_validation_and_diagnostics()
    {
        // Arrange
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var validator = SpyPipelineValidator.WithNoErrors();
        var diagnosticWriter = new SpyPipelineDiagnosticWriter();
        var logger = NullLogger<BrighterValidationHostedService>.Instance;
        var service = new BrighterValidationHostedService(options, validator, diagnosticWriter, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — both validation and diagnostics should have been called
        Assert.True(validator.ValidateWasCalled);
        Assert.True(diagnosticWriter.DescribeWasCalled);
    }

    [Fact]
    public async Task When_consumer_owns_validation_should_be_noop()
    {
        // Arrange
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = true });
        var validator = SpyPipelineValidator.WithNoErrors();
        var diagnosticWriter = new SpyPipelineDiagnosticWriter();
        var logger = NullLogger<BrighterValidationHostedService>.Instance;
        var service = new BrighterValidationHostedService(options, validator, diagnosticWriter, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — neither validation nor diagnostics should have run
        Assert.False(validator.ValidateWasCalled);
        Assert.False(diagnosticWriter.DescribeWasCalled);
    }

    [Fact]
    public async Task When_validation_has_errors_should_throw_pipeline_validation_exception()
    {
        // Arrange
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = SpyPipelineValidator.WithErrors(error);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter();
        var logger = NullLogger<BrighterValidationHostedService>.Instance;
        var service = new BrighterValidationHostedService(options, validator, diagnosticWriter, logger);

        // Act & Assert — validation errors should prevent startup
        await Assert.ThrowsAsync<PipelineValidationException>(
            () => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task When_validation_has_warnings_only_should_not_throw()
    {
        // Arrange
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var warning = new ValidationError(ValidationSeverity.Warning, "TestHandler", "Backstop ordering suboptimal");
        var validator = SpyPipelineValidator.WithWarningsOnly(warning);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter();
        var logger = NullLogger<BrighterValidationHostedService>.Instance;
        var service = new BrighterValidationHostedService(options, validator, diagnosticWriter, logger);

        // Act — should complete without throwing
        await service.StartAsync(CancellationToken.None);

        // Assert — validation ran, but no exception was thrown
        Assert.True(validator.ValidateWasCalled);
        Assert.True(diagnosticWriter.DescribeWasCalled);
    }
}
