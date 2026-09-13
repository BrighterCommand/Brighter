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
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Evaluates ADR 0074's scope-configuration rules — the lifetime, opt-in and captive-dependency rules
    /// the container-backed factories' behaviour depends on — and reports them through the same
    /// <see cref="IAmAPipelineValidator"/> seam as the core <c>PipelineValidator</c>. Registered alongside
    /// it, not wrapping it: both validation hosts resolve every registered <see cref="IAmAPipelineValidator"/>
    /// and combine the results.
    /// </summary>
    /// <remarks>
    /// Public because it is one of the implementations <see cref="IAmAPipelineValidator"/> now resolves to;
    /// its constructor is <c>internal</c> because C# forbids a public constructor whose parameter types are
    /// less accessible (CS0051) — <see cref="ContainerRegistrationSnapshot"/> is internal, and the only call
    /// site is the registration delegate in this same assembly. Evaluates over core's public
    /// <see cref="ISpecification{T}"/>, <see cref="Specification{T}"/> and
    /// <see cref="ValidationResultCollector{T}"/> with its own harvest loop —
    /// <c>PipelineValidator.EvaluateSpecs</c> is not extracted, moved or widened, since there is no
    /// <c>InternalsVisibleTo</c> anywhere and lifting it would put permanent public API on core's
    /// <c>netstandard2.0</c> surface. Runs nothing else and disposes nothing — the
    /// <see cref="MessageMapperRegistry"/> the captive-dependency check reads transforms from is owned and
    /// disposed by the shared <see cref="ValidationMapperRegistry"/>, not by this type.
    /// </remarks>
    public sealed class ScopeConfigurationValidator : IAmAPipelineValidator
    {
        // null when IBrighterOptions was never registered — a host built directly on
        // ServiceCollectionBrighterBuilder rather than through AddBrighter/AddConsumers. There is then no
        // scope configuration to validate, exactly as PipelineValidator leaves its own optional rules
        // inert when their inputs are absent (e.g. ValidateProducers when publications is null).
        private readonly ScopeConfiguration? _configuration;
        private readonly ContainerRegistrationSnapshot _snapshot;
        private readonly PipelineBuilder<IRequest> _pipelineBuilder;
        private readonly IEnumerable<Publication>? _publications;
        private readonly IEnumerable<Subscription>? _subscriptions;
        private readonly ValidationMapperRegistry _mapperRegistry;

        internal ScopeConfigurationValidator(
            IBrighterOptions? options,
            ContainerRegistrationSnapshot snapshot,
            PipelineBuilder<IRequest> pipelineBuilder,
            IEnumerable<Publication>? publications,
            IEnumerable<Subscription>? subscriptions,
            ValidationMapperRegistry mapperRegistry)
        {
            _configuration = options is null
                ? null
                : new ScopeConfiguration(
                    options.DefaultScopeAffinity,
                    options.HandlerLifetime,
                    options.MapperLifetime,
                    options.TransformerLifetime,
                    snapshot.DescriptorsFor(typeof(IAmAScopeProvider)),
                    snapshot.DescriptorsFor(typeof(ScopeAffinityOverride)),
                    snapshot.DescriptorsFor(typeof(IBrighterOptions)));
            _snapshot = snapshot;
            _pipelineBuilder = pipelineBuilder;
            _publications = publications;
            _subscriptions = subscriptions;
            _mapperRegistry = mapperRegistry;
        }

        /// <inheritdoc />
        public PipelineValidationResult Validate()
        {
            var findings = new List<ValidationError>();

            if (_configuration is not null)
            {
                var configurationSpecs = new List<ISpecification<ScopeConfiguration>>
                {
                    ScopeConfigurationRules.InertOptIn(),
                    ScopeConfigurationRules.MixedLifetimes()
                };
                EvaluateSpecs(new[] { _configuration }, configurationSpecs, findings);

                var excluded = ArtefactExclusionSet.Build(
                    _pipelineBuilder, _mapperRegistry.Value, _publications, _subscriptions);
                var candidates = _snapshot.Artefacts(
                    _configuration.HandlerLifetime, _configuration.MapperLifetime, _configuration.TransformerLifetime);
                var captiveDependencySpecs = new List<ISpecification<ArtefactRegistration>>
                {
                    ScopeConfigurationRules.CaptiveDependency(_snapshot, excluded)
                };
                EvaluateSpecs(candidates, captiveDependencySpecs, findings);
            }

            var distinctFindings = findings.Distinct().ToList();
            var errors = distinctFindings.Where(f => f.Severity == ValidationSeverity.Error);
            var warnings = distinctFindings.Where(f => f.Severity == ValidationSeverity.Warning);
            return new PipelineValidationResult(errors, warnings);
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
}
