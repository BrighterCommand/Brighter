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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ServiceActivatorThrowOnErrorTests
{
    [Fact]
    public async Task When_throw_on_error_false_and_errors_should_log_and_still_receive()
    {
        // Arrange
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = SpyPipelineValidator.WithErrors(actionLog, error);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter(actionLog);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        services.AddSingleton<IAmAPipelineDiagnosticWriter>(diagnosticWriter);
        var provider = services.BuildServiceProvider();

        var logger = new SpyLogger<ServiceActivatorHostedService>();
        var options = Options.Create(new BrighterPipelineValidationOptions
        {
            ConsumerOwnsValidation = true,
            ThrowOnError = false
        });
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act — should NOT throw, should still call Receive
        await service.StartAsync(CancellationToken.None);

        // Assert — errors logged, Receive was called
        Assert.True(validator.ValidateWasCalled);
        Assert.True(dispatcher.ReceiveWasCalled);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("misconfigured"));
    }

    [Fact]
    public async Task When_throw_on_error_true_and_errors_should_throw_and_not_receive()
    {
        // Arrange
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = SpyPipelineValidator.WithErrors(actionLog, error);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter(actionLog);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        services.AddSingleton<IAmAPipelineDiagnosticWriter>(diagnosticWriter);
        var provider = services.BuildServiceProvider();

        var logger = new SpyLogger<ServiceActivatorHostedService>();
        var options = Options.Create(new BrighterPipelineValidationOptions
        {
            ConsumerOwnsValidation = true,
            ThrowOnError = true
        });
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act & Assert — should throw, Receive should NOT be called
        await Assert.ThrowsAsync<PipelineValidationException>(
            () => service.StartAsync(CancellationToken.None));
        Assert.False(dispatcher.ReceiveWasCalled);
    }

    [Fact]
    public async Task When_throw_on_error_false_and_no_errors_should_receive_normally()
    {
        // Arrange
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);
        var validator = SpyPipelineValidator.WithNoErrors(actionLog);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter(actionLog);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        services.AddSingleton<IAmAPipelineDiagnosticWriter>(diagnosticWriter);
        var provider = services.BuildServiceProvider();

        var logger = new SpyLogger<ServiceActivatorHostedService>();
        var options = Options.Create(new BrighterPipelineValidationOptions
        {
            ConsumerOwnsValidation = true,
            ThrowOnError = false
        });
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — no errors, Receive called normally
        Assert.True(validator.ValidateWasCalled);
        Assert.True(dispatcher.ReceiveWasCalled);
        Assert.DoesNotContain(logger.Entries, e => e.LogLevel == LogLevel.Error);
    }

    [Fact]
    public async Task When_throw_on_error_false_should_still_log_warnings()
    {
        // Arrange
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);
        var warning = new ValidationError(ValidationSeverity.Warning, "TestHandler", "Backstop ordering suboptimal");
        var error = new ValidationError(ValidationSeverity.Error, "TestHandler", "Handler is misconfigured");
        var validator = new SpyPipelineValidator(new PipelineValidationResult([error], [warning]), actionLog);
        var diagnosticWriter = new SpyPipelineDiagnosticWriter(actionLog);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        services.AddSingleton<IAmAPipelineDiagnosticWriter>(diagnosticWriter);
        var provider = services.BuildServiceProvider();

        var logger = new SpyLogger<ServiceActivatorHostedService>();
        var options = Options.Create(new BrighterPipelineValidationOptions
        {
            ConsumerOwnsValidation = true,
            ThrowOnError = false
        });
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — both errors and warnings logged
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("misconfigured"));
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning && e.Message.Contains("suboptimal"));
        Assert.True(dispatcher.ReceiveWasCalled);
    }
}
