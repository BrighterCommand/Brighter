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
using System.Threading;

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
/// <param name="inbox">Optional inbox the runtime pipeline uses; passed to rules that check causation tracking.</param>
/// <param name="outbox">Optional outbox the runtime pipeline uses; passed to rules that check causation tracking.</param>
/// <param name="providerRegistrations">Optional validation-provider registrations. When supplied, the
/// validation-provider check runs over handler pipelines; null (the default) leaves it inert.</param>
/// <param name="mapperRegistryFactory">Optional factory that builds the mapper registry used to describe a
/// publication's transforms. The validator invokes it at most once — lazily, the first time a validation
/// rule needs the registry — and takes ownership of the registry it returns, disposing it at teardown only
/// if it was built. Taking a factory rather than a live instance keeps that ownership transfer
/// explicit — the validator disposes only a registry it created — so a caller cannot hand in a registry it
/// still uses elsewhere and have it disposed underneath them. Together with <paramref name="transformerProbe"/>
/// it enables the producer wrap-transform check.</param>
/// <param name="transformerProbe">Optional probe answering whether a declared transformer type is resolvable.
/// Together with <paramref name="mapperRegistryFactory"/> it enables the producer wrap-transform check.</param>
public class PipelineValidator(
    PipelineBuilder<IRequest> pipelineBuilder,
    IEnumerable<Publication>? publications = null,
    IEnumerable<Subscription>? subscriptions = null,
    IEnumerable<ISpecification<Subscription>>? consumerSpecs = null,
    IAmAnInbox? inbox = null,
    IAmAnOutbox? outbox = null,
    ValidationProviderRegistrations? providerRegistrations = null,
    Func<MessageMapperRegistry>? mapperRegistryFactory = null,
    IAmATransformerResolvabilityProbe? transformerProbe = null) : IAmAPipelineValidator, IDisposable
{
    //built lazily and at most once: the wrap-transform check may never run (no transformer probe, or no
    //publications to validate against), so a factory supplied at construction must not build the registry —
    //and its two mapper factories — until a rule actually needs it. The validator owns the registry it
    //produces and disposes only that one, only if it was built.
    private readonly Lazy<MessageMapperRegistry>? _mapperRegistry = mapperRegistryFactory is null
        ? null
        : new Lazy<MessageMapperRegistry>(mapperRegistryFactory);

    //an int rather than a bool so Dispose can claim it with a single atomic Interlocked.Exchange:
    //an owner and the container disposing concurrently then dispose the registry exactly once
    private int _disposed;

    /// <summary>
    /// Disposes the mapper registry this validator built for the wrap-transform check.
    /// </summary>
    /// <remarks>
    /// The validator is a singleton that owns its validation-time <see cref="MessageMapperRegistry"/>; the
    /// container disposes the validator at shutdown. Cascading to the registry drains the mapper factory (and
    /// any scope it holds) rather than retaining it until the process exits. Idempotent.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        //only a registry that was actually built needs draining; if no rule ever needed it, there is
        //nothing to dispose
        if (_mapperRegistry is { IsValueCreated: true })
            _mapperRegistry.Value.Dispose();
    }

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
        var descriptions = pipelineBuilder.Describe();
        var specs = new List<ISpecification<HandlerPipelineDescription>>
        {
            HandlerPipelineValidationRules.HandlerTypeVisibility(),
            HandlerPipelineValidationRules.BackstopAttributeOrdering(),
            HandlerPipelineValidationRules.AttributeAsyncConsistency(),
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox)
        };

        if (providerRegistrations is not null)
            specs.Add(HandlerPipelineValidationRules.ValidationProviderRegistered(providerRegistrations));

        EvaluateSpecs(descriptions, specs, findings);
    }

    private void ValidateProducers(List<ValidationError> findings)
    {
        if (publications == null) return;

        var specs = new List<ISpecification<Publication>>
        {
            ProducerValidationRules.PublicationRequestTypeSet(),
            ProducerValidationRules.PublicationRequestTypeImplementsIRequest()
        };

        //accessing .Value here builds the registry — the first and only point a rule needs it
        if (_mapperRegistry is not null && transformerProbe is not null)
            specs.Add(ProducerValidationRules.WrapTransformResolvable(_mapperRegistry.Value, transformerProbe));

        EvaluateSpecs(publications, specs, findings);
    }

    private void ValidateConsumers(List<ValidationError> findings)
    {
        if (subscriptions == null || consumerSpecs == null) return;

        EvaluateSpecs(subscriptions, consumerSpecs, findings);
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
