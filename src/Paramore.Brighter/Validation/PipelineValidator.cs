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

namespace Paramore.Brighter.Validation;

/// <summary>
/// Evaluates all registered validation rule sets and aggregates findings into a
/// <see cref="PipelineValidationResult"/>. Validates handler pipelines unconditionally,
/// and optionally validates producers and consumers when their configuration is provided.
/// </summary>
/// <param name="pipelineBuilder">The pipeline builder used to describe handler pipelines.</param>
/// <param name="publications">Optional publications to validate against producer rules.</param>
/// <param name="subscriptions">Optional subscriptions to validate against consumer rules.</param>
/// <param name="consumerSpecs">Optional consumer validation specifications.</param>
public class PipelineValidator(
    PipelineBuilder<IRequest> pipelineBuilder,
    IEnumerable<Publication>? publications = null,
    IEnumerable<Subscription>? subscriptions = null,
    IEnumerable<ISpecification<Subscription>>? consumerSpecs = null) : IAmAPipelineValidator
{
    private readonly PipelineBuilder<IRequest> _pipelineBuilder = pipelineBuilder;
    private readonly IEnumerable<Publication>? _publications = publications;
    private readonly IEnumerable<Subscription>? _subscriptions = subscriptions;
    private readonly IEnumerable<ISpecification<Subscription>>? _consumerSpecs = consumerSpecs;

    /// <inheritdoc />
    public PipelineValidationResult Validate()
    {
        var findings = new List<ValidationError>();

        ValidateHandlerPipelines(findings);
        ValidateProducers(findings);
        ValidateConsumers(findings);

        var errors = findings.Where(f => f.Severity == ValidationSeverity.Error);
        var warnings = findings.Where(f => f.Severity == ValidationSeverity.Warning);

        return new PipelineValidationResult(errors, warnings);
    }

    private void ValidateHandlerPipelines(List<ValidationError> findings)
    {
        var descriptions = _pipelineBuilder.Describe();
        var specs = new ISpecification<HandlerPipelineDescription>[]
        {
            HandlerPipelineValidationRules.HandlerTypeVisibility(),
            HandlerPipelineValidationRules.BackstopAttributeOrdering(),
            HandlerPipelineValidationRules.AttributeAsyncConsistency()
        };

        EvaluateSpecs(descriptions, specs, findings);
    }

    private void ValidateProducers(List<ValidationError> findings)
    {
        if (_publications == null) return;

        var specs = new ISpecification<Publication>[]
        {
            ProducerValidationRules.PublicationRequestTypeSet(),
            ProducerValidationRules.PublicationRequestTypeImplementsIRequest()
        };

        EvaluateSpecs(_publications, specs, findings);
    }

    private void ValidateConsumers(List<ValidationError> findings)
    {
        if (_subscriptions == null || _consumerSpecs == null) return;

        EvaluateSpecs(_subscriptions, _consumerSpecs, findings);
    }

    private static void EvaluateSpecs<T>(
        IEnumerable<T> entities,
        IEnumerable<ISpecification<T>> specs,
        List<ValidationError> findings)
    {
        var collector = new ValidationResultCollector<T>();
        foreach (var entity in entities)
        {
            foreach (var spec in specs)
            {
                if (!spec.IsSatisfiedBy(entity))
                {
                    findings.AddRange(
                        spec.Accept(collector)
                            .Where(r => !r.Success)
                            .Select(r => r.Error!));
                }
            }
        }
    }
}
