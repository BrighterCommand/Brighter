#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesProviderRegistrationTests
{
    [Fact]
    public void When_validation_step_present_and_no_provider_through_di_should_surface_warning()
    {
        // Arrange — a handler whose pipeline declares a validation step ([ValidateRequest]) but no
        // validation provider is registered; ValidatePipelines must compute the (false,false)
        // registrations from the service collection and thread them into the validator.
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        subscriberRegistry.Register<MyValidatedCommand, MyValidatedSyncHandler>();
        services.AddSingleton(subscriberRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.ValidatePipelines();

        var provider = services.BuildServiceProvider();

        // Act — resolve the validator and run validation through the full DI path
        var validator = provider.GetRequiredService<IAmAPipelineValidator>();
        var result = validator.Validate();

        // Assert — a (B) Warning surfaces naming the request and the three provider calls; warnings never block
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w =>
            w.Message.Contains(nameof(MyValidatedCommand))
            && w.Message.Contains("UseFluentValidation")
            && w.Message.Contains("UseDataAnnotations")
            && w.Message.Contains("UseSpecification"));
    }
}
