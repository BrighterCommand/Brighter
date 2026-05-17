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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class DoubleDescribePreventionTests
{
    [Fact]
    public async Task When_both_validate_and_describe_registered_should_describe_once()
    {
        // Arrange — a shared diagnostic writer registered in DI, used by both hosted services
        var diagnosticWriter = new SpyPipelineDiagnosticWriter();
        var validator = SpyPipelineValidator.WithNoErrors();
        var options = Options.Create(new BrighterPipelineValidationOptions { ConsumerOwnsValidation = false });

        var services = new ServiceCollection();
        services.AddSingleton<IAmAPipelineDiagnosticWriter>(diagnosticWriter);
        var provider = services.BuildServiceProvider();

        var validationService = new BrighterValidationHostedService(
            options, validator, provider, NullLogger<BrighterValidationHostedService>.Instance);
        var diagnosticService = new BrighterDiagnosticHostedService(diagnosticWriter, options);

        // Act — both hosted services start (as they would in a real host)
        await validationService.StartAsync(CancellationToken.None);
        await diagnosticService.StartAsync(CancellationToken.None);

        // Assert — Describe should have been called exactly once, not twice
        Assert.Equal(1, diagnosticWriter.DescribeCallCount);
    }
}
