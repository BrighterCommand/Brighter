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

using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.RequestValidation.Handlers;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Provides validation specifications for handler pipelines. Each method returns
/// an <see cref="ISpecification{T}"/> that evaluates a <see cref="HandlerPipelineDescription"/>
/// and reports validation findings via the visitor pattern.
/// </summary>
public static class HandlerPipelineValidationRules
{
    /// <summary>
    /// Validates that the handler type is publicly visible. Internal or private handler types
    /// cannot be found by the pipeline builder at runtime. <see cref="System.Type.IsVisible"/>
    /// accounts for both top-level non-public types and public types nested inside non-public parents.
    /// </summary>
    /// <returns>A simple specification that reports an Error when the handler type is not public.</returns>
    public static ISpecification<HandlerPipelineDescription> HandlerTypeVisibility()
        => new Specification<HandlerPipelineDescription>(
            d => d.HandlerType.IsVisible,
            d => new ValidationError(
                ValidationSeverity.Error,
                $"Handler '{d.HandlerType.Name}'",
                $"Handler type '{d.HandlerType.FullName}' is not public — " +
                "Brighter only supports public handler types. Make the class public " +
                "so the pipeline builder can find it"));

    /// <summary>
    /// Validates that backstop handlers (e.g. RejectMessageOnError) have lower step numbers
    /// than resilience handlers (e.g. UseResiliencePipeline). In Brighter, lower step values
    /// are outer wrappers, so a backstop with a higher step number than a resilience handler
    /// will never execute on failure. Reports a Warning per misordered pair.
    /// </summary>
    /// <returns>A collapsed specification that yields one Warning per misordered backstop/resilience pair.</returns>
    public static ISpecification<HandlerPipelineDescription> BackstopAttributeOrdering()
        => new Specification<HandlerPipelineDescription>(d =>
        {
            var backstops = d.BeforeSteps.Where(s =>
                typeof(IAmABackstopHandler).IsAssignableFrom(s.HandlerType));
            var resilience = d.BeforeSteps.Where(s =>
                typeof(IAmAResilienceHandler).IsAssignableFrom(s.HandlerType));

            return backstops.SelectMany(b => resilience
                .Where(r => b.Step > r.Step)
                .Select(r => ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    $"Handler '{d.HandlerType.Name}'",
                    $"'{b.AttributeType.Name}' at step {b.Step} is after " +
                    $"'{r.AttributeType.Name}' at step {r.Step} — " +
                    "in Brighter, lower step values are outer wrappers, so the backstop " +
                    "will never execute on failure"))));
        });

    /// <summary>
    /// Validates that each pipeline step's handler type matches the sync/async nature of the
    /// main handler. An async handler with a sync attribute step (or vice versa) means the step
    /// will throw a ConfigurationException at pipeline build time. Handler types that implement neither
    /// <see cref="IHandleRequests"/> nor <see cref="IHandleRequestsAsync"/> are reported as errors.
    /// </summary>
    /// <returns>A collapsed specification that yields one Error per mismatched or unrecognized step.</returns>
    public static ISpecification<HandlerPipelineDescription> AttributeAsyncConsistency()
        => new Specification<HandlerPipelineDescription>(d =>
        {
            return d.BeforeSteps.Concat(d.AfterSteps).SelectMany<PipelineStepDescription, ValidationResult>(step =>
            {
                var isSync = typeof(IHandleRequests).IsAssignableFrom(step.HandlerType);
                var isAsync = typeof(IHandleRequestsAsync).IsAssignableFrom(step.HandlerType);

                if (!isSync && !isAsync)
                {
                    return [ValidationResult.Fail(new ValidationError(
                        ValidationSeverity.Error,
                        $"Handler '{d.HandlerType.Name}'",
                        $"Pipeline step '{step.HandlerType.FullName}' at step {step.Step} " +
                        "implements neither IHandleRequests nor IHandleRequestsAsync"))];
                }

                if (d.IsAsync != isAsync)
                {
                    return [ValidationResult.Fail(new ValidationError(
                        ValidationSeverity.Error,
                        $"Handler '{d.HandlerType.Name}'",
                        d.IsAsync
                            ? $"Async handler uses sync attribute '{step.AttributeType.Name}' " +
                              $"at step {step.Step} — this will throw a ConfigurationException at pipeline build time"
                            : $"Sync handler uses async attribute '{step.AttributeType.Name}' " +
                              $"at step {step.Step} — this will throw a ConfigurationException at pipeline build time"))];
                }

                return [];
            });
        });

    /// <summary>
    /// Validates that a pipeline configuring <see cref="OnceOnlyAction.Replay"/> on a <c>UseInbox</c> step has
    /// the causation-tracking support Replay needs. On a duplicate, Replay reads the causation id of the original
    /// handling from the inbox and resets the dispatched state of that causation's outbox messages so the sweeper
    /// resends them; both stores must therefore track causation.
    /// </summary>
    /// <param name="inbox">The inbox the runtime pipeline uses, captured via closure; null when none is configured.</param>
    /// <param name="outbox">The outbox the runtime pipeline uses, captured via closure; null when none is configured.</param>
    /// <returns>
    /// A collapsed specification that yields, for each pipeline configuring Replay: an Error when the inbox or
    /// outbox does not implement the causation-tracking role; a Warning when the role is implemented but the live
    /// store schema does not support causation tracking, or when no outbox is configured (Replay is then a graceful
    /// terminal step). Non-Replay pipelines yield no findings.
    /// </returns>
    public static ISpecification<HandlerPipelineDescription> ReplayRequiresCausationTracking(
        IAmAnInbox? inbox, IAmAnOutbox? outbox)
        => new Specification<HandlerPipelineDescription>(d =>
        {
            var configuresReplay = d.BeforeSteps.Concat(d.AfterSteps)
                .Any(step => OnceOnlyActionOf(step.Attribute) == OnceOnlyAction.Replay);

            if (!configuresReplay) return [];

            var source = $"Handler '{d.HandlerType.Name}'";
            var findings = new List<ValidationResult>();

            if (inbox is not IAmACausationTrackingInbox trackingInbox)
            {
                findings.Add(ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Error,
                    source,
                    $"OnceOnlyAction.Replay requires a causation-tracking inbox, but the configured inbox " +
                    $"'{inbox?.GetType().Name ?? "(none)"}' does not implement IAmACausationTrackingInbox — " +
                    "Replay cannot find the causation id of the original handling")));
            }
            else if (!trackingInbox.SupportsCausationTracking())
            {
                findings.Add(ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    source,
                    "OnceOnlyAction.Replay requires causation tracking, but the inbox store schema does not " +
                    "support it — migrate the inbox schema to add the CausationId column for Replay to work")));
            }

            if (outbox is null)
            {
                findings.Add(ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    source,
                    "OnceOnlyAction.Replay has no outbox to replay — on a duplicate the handler is skipped and " +
                    "no messages are resent (Replay is a graceful terminal step without an outbox)")));
            }
            else if (outbox is not IAmACausationTrackingOutbox trackingOutbox)
            {
                findings.Add(ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Error,
                    source,
                    $"OnceOnlyAction.Replay requires a causation-tracking outbox, but the configured outbox " +
                    $"'{outbox.GetType().Name}' does not implement IAmACausationTrackingOutbox — " +
                    "Replay cannot reset the dispatched state of the original messages")));
            }
            else if (!trackingOutbox.SupportsCausationTracking())
            {
                findings.Add(ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    source,
                    "OnceOnlyAction.Replay requires causation tracking, but the outbox store schema does not " +
                    "support it — migrate the outbox schema to add the CausationId column for Replay to work")));
            }

            return findings;
        });

    private static OnceOnlyAction? OnceOnlyActionOf(RequestHandlerAttribute? attribute)
        => attribute switch
        {
            UseInboxAttribute sync => sync.OnceOnlyAction,
            UseInboxAsyncAttribute async => async.OnceOnlyAction,
            _ => null
        };

    /// <summary>
    /// Validates that a handler declaring a validation pipeline step has a validation provider registered.
    /// A step whose handler type is the open generic <see cref="ValidateRequestHandler{TRequest}"/> (sync) or
    /// <see cref="ValidateRequestHandlerAsync{TRequest}"/> (async) is backed only when the matching flag in
    /// <paramref name="registrations"/> is set; otherwise the abstract handler cannot be resolved when the
    /// request flows through the pipeline. Reports one
    /// <see cref="ValidationSeverity.Warning"/> per unbacked validation step, naming the request/handler and the
    /// three provider registration calls. Detection is provider-agnostic — it keys off the step's handler type
    /// (the attribute's <c>GetHandlerType()</c> target), not the concrete attribute type.
    /// </summary>
    /// <param name="registrations">Whether the sync and/or async validation provider is registered.</param>
    /// <returns>A collapsed specification that yields a Warning per validation step with no matching provider.</returns>
    public static ISpecification<HandlerPipelineDescription> ValidationProviderRegistered(ValidationProviderRegistrations registrations)
        => new Specification<HandlerPipelineDescription>(d =>
            d.BeforeSteps.Concat(d.AfterSteps)
                .Where(step => IsUnbackedValidationStep(step.HandlerType, registrations))
                .Select(_ => ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Warning,
                    $"Handler '{d.HandlerType.Name}'",
                    $"Request '{d.RequestType.Name}' declares a validation step but no validation provider is " +
                    "registered. Call UseFluentValidation(), UseDataAnnotations(), or UseSpecification().")))
                .ToList());

    /// <summary>
    /// Returns true when <paramref name="stepHandlerType"/> is a validation handler open generic
    /// whose matching provider flag in <paramref name="registrations"/> is not set.
    /// </summary>
    private static bool IsUnbackedValidationStep(Type stepHandlerType, ValidationProviderRegistrations registrations)
        => (stepHandlerType == typeof(ValidateRequestHandler<>) && !registrations.Sync)
           || (stepHandlerType == typeof(ValidateRequestHandlerAsync<>) && !registrations.Async);
}
