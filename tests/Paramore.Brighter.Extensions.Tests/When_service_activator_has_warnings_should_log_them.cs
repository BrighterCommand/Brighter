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
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ServiceActivatorWarningLoggingTests
{
    [Fact]
    public async Task When_service_activator_has_warnings_should_log_them_at_warning_level()
    {
        // Arrange — warnings only, no errors
        var actionLog = new List<string>();
        var warning1 = new ValidationError(ValidationSeverity.Warning, "HandlerA", "Backstop ordering suboptimal");
        var warning2 = new ValidationError(ValidationSeverity.Warning, "HandlerB", "Attribute mismatch suggestion");
        var dispatcher = new SpyDispatcher(actionLog);
        var validator = SpyPipelineValidator.WithWarningsOnly(actionLog, warning1, warning2);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineValidator>(validator);
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = true });
        var logger = new SpyLogger<ServiceActivatorHostedService>();
        var service = new ServiceActivatorHostedService(logger, dispatcher, provider, options);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — each warning is logged at Warning level with source and message
        var warningEntries = logger.WarningEntries.ToList();
        Assert.Equal(2, warningEntries.Count);
        Assert.Contains("HandlerA", warningEntries[0].Message);
        Assert.Contains("Backstop ordering suboptimal", warningEntries[0].Message);
        Assert.Contains("HandlerB", warningEntries[1].Message);
        Assert.Contains("Attribute mismatch suggestion", warningEntries[1].Message);

        // Assert — Receive was still called (warnings don't prevent startup)
        Assert.True(dispatcher.ReceiveWasCalled);
    }
}
