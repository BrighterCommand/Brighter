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
using System.Linq;

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

}
