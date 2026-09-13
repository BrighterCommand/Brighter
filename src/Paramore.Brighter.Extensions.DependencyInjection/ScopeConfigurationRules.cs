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
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Validation specifications for <see cref="ScopeConfiguration"/> and <see cref="ArtefactRegistration"/>,
    /// evaluated by <see cref="ScopeConfigurationValidator"/> (ADR 0074 step 4).
    /// </summary>
    /// <remarks>
    /// Carries the FR-22.1, FR-22.2 and FR-22.3 rules. The other four ADR 0074 rules arrive with their own
    /// acceptance criteria in later tasks — they are not stubbed here.
    /// </remarks>
    internal static class ScopeConfigurationRules
    {
        /// <summary>
        /// FR-22.1 — Error, inert opt-in. The affinity option is <see cref="ScopeAffinity.JoinAmbient"/>
        /// and none of <see cref="ScopeConfiguration.HandlerLifetime"/>,
        /// <see cref="ScopeConfiguration.MapperLifetime"/>, <see cref="ScopeConfiguration.TransformerLifetime"/>
        /// is <see cref="ServiceLifetime.Scoped"/> — so the opt-in has no effect (D5).
        /// </summary>
        /// <returns>A simple specification reporting an Error naming the affinity setting, all three
        /// lifetimes with their values, and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> InertOptIn()
            => new Specification<ScopeConfiguration>(
                c => !(c.Affinity == ScopeAffinity.JoinAmbient
                       && c.HandlerLifetime != ServiceLifetime.Scoped
                       && c.MapperLifetime != ServiceLifetime.Scoped
                       && c.TransformerLifetime != ServiceLifetime.Scoped),
                c => new ValidationError(
                    ValidationSeverity.Error,
                    "Brighter options",
                    $"DefaultScopeAffinity is {c.Affinity} but none of HandlerLifetime ({c.HandlerLifetime}), " +
                    $"MapperLifetime ({c.MapperLifetime}), TransformerLifetime ({c.TransformerLifetime}) is " +
                    $"{ServiceLifetime.Scoped} — the opt-in has no effect, because {ScopeAffinity.JoinAmbient} " +
                    $"only applies to a {ServiceLifetime.Scoped} handler, mapper or transformer. " +
                    "See docs/guides/lifetimes-and-scoping.md for guidance on choosing a conformant triple."));

        /// <summary>
        /// FR-22.2 — Error, mixed lifetimes. After discarding any of
        /// <see cref="ScopeConfiguration.HandlerLifetime"/>, <see cref="ScopeConfiguration.MapperLifetime"/>,
        /// <see cref="ScopeConfiguration.TransformerLifetime"/> that is <see cref="ServiceLifetime.Singleton"/>,
        /// the remainder must all be equal (D8). Not conditional on <see cref="ScopeConfiguration.Affinity"/> —
        /// a mixed pair can never share a pipeline-scoped dependency, regardless of whether either side ever
        /// joins an ambient scope.
        /// </summary>
        /// <returns>A simple specification reporting an Error naming all three lifetimes with their values
        /// and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> MixedLifetimes()
            => new Specification<ScopeConfiguration>(
                c =>
                {
                    var remainder = new[] { c.HandlerLifetime, c.MapperLifetime, c.TransformerLifetime }
                        .Where(lifetime => lifetime != ServiceLifetime.Singleton)
                        .Distinct()
                        .Count();
                    return remainder <= 1;
                },
                c => new ValidationError(
                    ValidationSeverity.Error,
                    "Brighter options",
                    $"HandlerLifetime ({c.HandlerLifetime}), MapperLifetime ({c.MapperLifetime}), " +
                    $"TransformerLifetime ({c.TransformerLifetime}) mix {ServiceLifetime.Transient} and " +
                    $"{ServiceLifetime.Scoped} — the mixed pair do not share pipeline-scoped dependencies. " +
                    "See docs/guides/lifetimes-and-scoping.md for guidance on choosing a conformant triple."));

        /// <summary>
        /// FR-22.3 — Warning, captive dependency. A candidate whose kind's configured lifetime is
        /// <see cref="ServiceLifetime.Singleton"/>, that is not Brighter's own (<paramref name="excluded"/>),
        /// and whose <see cref="ArtefactConstructorSelector"/>-selected constructor takes a direct parameter
        /// registered <see cref="ServiceLifetime.Scoped"/> in <paramref name="snapshot"/> (D15, C-20).
        /// </summary>
        /// <param name="snapshot">Reads each parameter's effective registered lifetime.</param>
        /// <param name="excluded">Types Brighter itself put in the pipeline (ADR 0074 step 5a) — never
        /// inspected.</param>
        /// <returns>A specification reporting one Warning per captive parameter, naming the artefact type,
        /// the dependency type, and the guidance page.</returns>
        public static ISpecification<ArtefactRegistration> CaptiveDependency(
            ContainerRegistrationSnapshot snapshot, HashSet<Type> excluded)
        {
            var selector = new ArtefactConstructorSelector();

            return new Specification<ArtefactRegistration>(candidate =>
            {
                if (candidate.ConfiguredLifetime != ServiceLifetime.Singleton)
                    return [];

                if (excluded.Contains(candidate.ArtefactType))
                    return [];

                var constructor = selector.Select(candidate.ArtefactType);
                if (constructor is null)
                    return [];

                var findings = new List<ValidationResult>();
                foreach (var parameter in constructor.GetParameters())
                {
                    var key = parameter.GetCustomAttribute<FromKeyedServicesAttribute>()?.Key;
                    if (snapshot.EffectiveLifetimeOf(parameter.ParameterType, key) != ServiceLifetime.Scoped)
                        continue;

                    findings.Add(ValidationResult.Fail(new ValidationError(
                        ValidationSeverity.Warning,
                        $"{candidate.Kind} '{candidate.ArtefactType.Name}'",
                        $"'{candidate.ArtefactType.Name}' is {ServiceLifetime.Singleton} but its constructor " +
                        $"requires '{parameter.ParameterType.Name}', which is registered {ServiceLifetime.Scoped} " +
                        "— a captive dependency. See docs/guides/lifetimes-and-scoping.md for guidance.")));
                }
                return findings;
            });
        }
    }
}
