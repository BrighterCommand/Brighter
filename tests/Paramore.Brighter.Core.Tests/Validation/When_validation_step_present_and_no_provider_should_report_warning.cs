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
using System.Threading.Tasks;

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

    [Test]
    public async Task When_validation_step_present_and_no_provider_should_report_warning()
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
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
        await Assert.That(results[0].Error!.Source).Contains(nameof(MyPublicAsyncHandler));
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableCommand));
        await Assert.That(results[0].Error!.Message).Contains("UseFluentValidation");
        await Assert.That(results[0].Error!.Message).Contains("UseDataAnnotations");
        await Assert.That(results[0].Error!.Message).Contains("UseSpecification");
    }

    [Test]
    public async Task When_async_validation_step_present_and_async_provider_registered_should_report_no_warning()
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
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_async_validation_step_present_and_only_sync_provider_registered_should_report_warning()
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
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
    }

    [Test]
    public async Task When_sync_validation_step_present_and_no_provider_should_report_warning()
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
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
        await Assert.That(results[0].Error!.Message).Contains(nameof(MyDescribableCommand));
    }

    [Test]
    public async Task When_sync_validation_step_present_and_only_async_provider_registered_should_report_warning()
    {
        // Arrange — sync validation step; only the ASYNC provider is registered, so the sync step is unbacked.
        // The mirror of the async-step/only-sync-provider case — the rule must match modality to the right flag.
        var description = DescriptionWith(
            typeof(ValidateRequestAttribute), typeof(ValidateRequestHandler<>), isAsync: false, typeof(MyPublicSyncHandler));
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: true));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
    }

    [Test]
    public async Task When_sync_validation_step_present_and_sync_provider_registered_should_report_no_warning()
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
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_no_validation_step_present_should_report_no_warning()
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
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_validation_step_present_in_after_steps_and_no_provider_should_report_warning()
    {
        // Arrange — the validation step appears as an after-step; the rule scans both before and after steps
        var description = new HandlerPipelineDescription(
            requestType: typeof(MyDescribableCommand),
            handlerType: typeof(MyPublicAsyncHandler),
            isAsync: true,
            beforeSteps: [],
            afterSteps:
            [
                new PipelineStepDescription(
                    typeof(ValidateRequestAsyncAttribute), typeof(ValidateRequestHandlerAsync<>), Step: 1, HandlerTiming.After)
            ]);
        var spec = HandlerPipelineValidationRules.ValidationProviderRegistered(
            new ValidationProviderRegistrations(Sync: false, Async: false));

        // Act
        var satisfied = spec.IsSatisfiedBy(description);
        var results = Evaluate(spec);

        // Assert — the after-step is detected and warned
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
    }
}