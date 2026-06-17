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

using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Reject.Attributes;
using Paramore.Brighter.Reject.Handlers;
using Paramore.Brighter.RequestValidation.Attributes;
using Paramore.Brighter.RequestValidation.Handlers;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidationProviderRegisteredTests
{
    private static HandlerPipelineDescription DescriptionWith(Type stepAttribute, Type stepHandler, bool isAsync, Type handlerType)
        => new(
            requestType: typeof(MyDescribableCommand),
            handlerType: handlerType,
            isAsync: isAsync,
            beforeSteps:
            [
                new PipelineStepDescription(stepAttribute, stepHandler, Step: 1, HandlerTiming.Before)
            ],
            afterSteps: []);

    private static List<ValidationResult> Evaluate(ISpecification<HandlerPipelineDescription> spec)
        => spec.Accept(new ValidationResultCollector<HandlerPipelineDescription>()).Where(r => !r.Success).ToList();

    [Fact]
    public void When_validation_step_present_and_no_provider_should_report_warning()
    {
        // Arrange — async handler with a step targeting ValidateRequestHandlerAsync<>, no provider registered
        var description = DescriptionWith(
            typeof(ValidateRequestAsyncAttribute), typeof(ValidateRequestHandlerAsync<>), isAsync: true, typeof(MyPublicAsyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert — one Warning naming the handler/request and the three provider calls
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains(nameof(MyPublicAsyncHandler), results[0].Error!.Source);
        Assert.Contains(nameof(MyDescribableCommand), results[0].Error!.Message);
        Assert.Contains("UseFluentValidation", results[0].Error!.Message);
        Assert.Contains("UseDataAnnotations", results[0].Error!.Message);
        Assert.Contains("UseSpecification", results[0].Error!.Message);
    }

    [Fact]
    public void When_async_validation_step_present_and_async_provider_registered_should_report_no_warning()
    {
        // Arrange — async validation step, async provider registered
        var description = DescriptionWith(
            typeof(ValidateRequestAsyncAttribute), typeof(ValidateRequestHandlerAsync<>), isAsync: true, typeof(MyPublicAsyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: true));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_async_validation_step_present_and_only_sync_provider_registered_should_report_warning()
    {
        // Arrange — async validation step; only the SYNC provider is registered, so the async step is unbacked.
        // The rule must match the step's modality to the matching flag (no transposition).
        var description = DescriptionWith(
            typeof(ValidateRequestAsyncAttribute), typeof(ValidateRequestHandlerAsync<>), isAsync: true, typeof(MyPublicAsyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: true, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
    }

    [Fact]
    public void When_sync_validation_step_present_and_no_provider_should_report_warning()
    {
        // Arrange — sync handler with a step targeting ValidateRequestHandler<>, no provider registered
        var description = DescriptionWith(
            typeof(ValidateRequestAttribute), typeof(ValidateRequestHandler<>), isAsync: false, typeof(MyPublicSyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Error!.Severity);
        Assert.Contains(nameof(MyDescribableCommand), results[0].Error!.Message);
    }

    [Fact]
    public void When_sync_validation_step_present_and_sync_provider_registered_should_report_no_warning()
    {
        // Arrange — sync validation step, sync provider registered
        var description = DescriptionWith(
            typeof(ValidateRequestAttribute), typeof(ValidateRequestHandler<>), isAsync: false, typeof(MyPublicSyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: true, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_no_validation_step_present_should_report_no_warning()
    {
        // Arrange — a non-validation pipeline step (RejectMessageOnError), no provider registered
        var description = DescriptionWith(
            typeof(RejectMessageOnErrorAttribute), typeof(RejectMessageOnErrorHandler<>), isAsync: false, typeof(MyPublicSyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
