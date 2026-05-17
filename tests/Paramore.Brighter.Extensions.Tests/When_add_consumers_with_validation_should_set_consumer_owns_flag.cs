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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class AddConsumersValidationFlagTests
{
    [Fact]
    public void When_validate_pipelines_then_add_consumers_should_set_consumer_owns_validation_true()
    {
        // Arrange
        var services = new ServiceCollection();
        var brighterBuilder = services.AddBrighter();

        // Act — ValidatePipelines first, then AddConsumers
        brighterBuilder.ValidatePipelines();
        services.AddConsumers();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>().Value;
        Assert.True(options.ConsumerOwnsValidation);
    }

    [Fact]
    public void When_add_consumers_then_validate_pipelines_should_set_consumer_owns_validation_true()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — AddConsumers first, then ValidatePipelines (order independent)
        var brighterBuilder = services.AddConsumers();
        brighterBuilder.ValidatePipelines();

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BrighterPipelineValidationOptions>>().Value;
        Assert.True(options.ConsumerOwnsValidation);
    }

    [Fact]
    public void When_add_consumers_without_validate_pipelines_should_not_register_validation_options()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act — AddConsumers only, no ValidatePipelines
        services.AddConsumers();

        // Assert — Options infrastructure registers defaults, but ConsumerOwnsValidation is true
        // because AddConsumers always configures it. The key behavior is that without
        // ValidatePipelines, no IAmAPipelineValidator is registered.
        var provider = services.BuildServiceProvider();
        var validator = provider.GetService<Paramore.Brighter.Validation.IAmAPipelineValidator>();
        Assert.Null(validator);
    }
}
