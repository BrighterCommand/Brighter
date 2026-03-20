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
using Microsoft.Extensions.Options;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidationHostedServiceWarningLoggingTests
{
    [Fact]
    public async Task When_validation_has_warnings_should_log_them_at_warning_level()
    {
        // Arrange — two warnings with distinct source and message
        var warning1 = new ValidationError(ValidationSeverity.Warning, "HandlerA", "Backstop ordering suboptimal");
        var warning2 = new ValidationError(ValidationSeverity.Warning, "HandlerB", "Attribute mismatch suggestion");
        var validator = SpyPipelineValidator.WithWarningsOnly(warning1, warning2);
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var logger = new SpyLogger<BrighterValidationHostedService>();
        var provider = new ServiceCollection().BuildServiceProvider();
        var service = new BrighterValidationHostedService(options, validator, provider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — each warning is logged at Warning level with source and message
        var warningEntries = logger.WarningEntries.ToList();
        Assert.Equal(2, warningEntries.Count);
        Assert.Contains("HandlerA", warningEntries[0].Message);
        Assert.Contains("Backstop ordering suboptimal", warningEntries[0].Message);
        Assert.Contains("HandlerB", warningEntries[1].Message);
        Assert.Contains("Attribute mismatch suggestion", warningEntries[1].Message);
    }

    [Fact]
    public async Task When_validation_has_no_warnings_should_not_log_warnings()
    {
        // Arrange — no warnings
        var validator = SpyPipelineValidator.WithNoErrors();
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });
        var logger = new SpyLogger<BrighterValidationHostedService>();
        var provider = new ServiceCollection().BuildServiceProvider();
        var service = new BrighterValidationHostedService(options, validator, provider, logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — no warning log entries
        Assert.Empty(logger.WarningEntries);
    }
}
