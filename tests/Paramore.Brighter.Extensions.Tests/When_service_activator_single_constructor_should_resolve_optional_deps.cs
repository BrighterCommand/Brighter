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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ServiceActivatorSingleConstructorTests
{
    [Fact]
    public async Task When_consumer_owns_validation_and_validator_registered_should_validate_before_receive()
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

        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = true });
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — validation and diagnostics run before Receive
        Assert.True(validator.ValidateWasCalled);
        Assert.True(diagnosticWriter.DescribeWasCalled);
        Assert.Equal(new List<string> { "Describe", "Validate", "Receive" }, actionLog);
    }

    [Fact]
    public async Task When_consumer_owns_validation_and_validator_not_registered_should_go_to_receive()
    {
        // Arrange — no validator or diagnostic writer registered
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);

        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = true });
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — straight to Receive, no error
        Assert.True(dispatcher.ReceiveWasCalled);
        Assert.Equal(new List<string> { "Receive" }, actionLog);
    }

    [Fact]
    public async Task When_consumer_does_not_own_validation_should_go_straight_to_receive()
    {
        // Arrange — consumer does not own validation; defers to BrighterValidationHostedService
        var actionLog = new List<string>();
        var dispatcher = new SpyDispatcher(actionLog);
        var validator = SpyPipelineValidator.WithNoErrors(actionLog);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var logger = NullLogger<ServiceActivatorHostedService>.Instance;
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — skips validation, goes straight to Receive
        Assert.False(validator.ValidateWasCalled);
        Assert.Equal(new List<string> { "Receive" }, actionLog);
    }
}
