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
    /// Carries all seven ADR 0074 rules: FR-22.1, FR-22.2, FR-22.3, FR-22.4, FR-24.3 and both of FR-17's.
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
        /// FR-22.4 — Error, defeated opt-in. An affinity override (<see cref="ScopeAffinityOverride"/>) is
        /// registered, and the <see cref="IBrighterOptions"/> descriptor the container will resolve — the
        /// last <b>unkeyed</b> one — is not the descriptor <c>RegisterBrighterOptions</c> (ADR 0076) added,
        /// so the write-through never ran and the override was never applied (D18). A rule about
        /// registrations, not values — it must not compare the override's affinity with the resolved
        /// object's, since an override carrying <see cref="ScopeAffinity.AlwaysNew"/> (the option's own
        /// default) is by value indistinguishable from an override that was never applied. Declines to
        /// fire when the override itself is unreadable (registered by factory delegate) — that shape is
        /// <see cref="UnreadableOverride"/>'s to report, and this rule has no value to name in its message.
        /// </summary>
        /// <returns>A simple specification reporting an Error naming the affinity the override carries,
        /// that the resolved <see cref="IBrighterOptions"/> was supplied by the application rather than by
        /// Brighter, the remedy, and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> DefeatedOptIn()
            => new Specification<ScopeConfiguration>(
                c =>
                {
                    var lastOverride = c.AffinityOverrideRegistrations.LastOrDefault();
                    if (lastOverride is null) return true; // no opt-in registered — nothing to defeat
                    if (lastOverride.ImplementationInstance is not ScopeAffinityOverride)
                        return true; // unreadable — UnreadableOverride() already reports this shape

                    var lastUnkeyedOptions = c.BrighterOptionsRegistrations.LastOrDefault(d => d.ServiceKey is null);
                    return lastUnkeyedOptions is not null && lastUnkeyedOptions.IsBrighterRegistered;
                },
                c =>
                {
                    var affinity = ((ScopeAffinityOverride)c.AffinityOverrideRegistrations.Last().ImplementationInstance!).Affinity;
                    return new ValidationError(
                        ValidationSeverity.Error,
                        "Brighter options registration",
                        $"AddBrighterRequestScope registered affinity {affinity}, but the resolved " +
                        $"{nameof(IBrighterOptions)} was supplied by the application rather than by Brighter, so " +
                        "the override was never applied. Configure Brighter's options through AddBrighter/" +
                        "AddConsumers instead of registering IBrighterOptions directly. " +
                        "See docs/guides/lifetimes-and-scoping.md for guidance.");
                });

        /// <summary>
        /// FR-24.3 — Warning, duplicate scope provider. More than one distinct <see cref="IAmAScopeProvider"/>
        /// implementation is registered unkeyed (D11 means Brighter itself never registers a default one, so
        /// two such registrations can only come from the application). MS DI resolves an unkeyed service type
        /// to the last-registered descriptor, so that is the provider the container-backed factories will
        /// actually ask; the rest are silently shadowed. Distinctness is judged over the <b>implementation</b>
        /// — a repeated registration of the same type is idempotent in effect and is not a finding.
        /// </summary>
        /// <returns>A simple specification reporting a Warning naming every distinct provider registered,
        /// identifying the last-registered one as effective, and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> DuplicateScopeProvider()
            => new Specification<ScopeConfiguration>(
                c => UnkeyedProviderIdentities(c).Distinct().Count() <= 1,
                c =>
                {
                    var identities = UnkeyedProviderIdentities(c).ToList();
                    var distinctIdentities = identities.Distinct().Select(DescribeProviderIdentity).ToList();
                    var effective = DescribeProviderIdentity(identities.Last());
                    return new ValidationError(
                        ValidationSeverity.Warning,
                        "Brighter options",
                        $"More than one {nameof(IAmAScopeProvider)} is registered ({string.Join(", ", distinctIdentities)}) " +
                        $"— the container resolves the last-registered one, {effective}, as the effective " +
                        "ambient scope source, and the others are silently shadowed. Register only one " +
                        "IAmAScopeProvider implementation. See docs/guides/lifetimes-and-scoping.md for guidance.");
                });

        private static IEnumerable<object> UnkeyedProviderIdentities(ScopeConfiguration configuration)
            => configuration.ScopeProviderRegistrations
                .Where(d => d.ServiceKey is null)
                .Select(d => (object?)d.ImplementationType ?? d.ImplementationInstance?.GetType() ?? (object)d.Position);

        private static string DescribeProviderIdentity(object identity)
            => identity is Type type ? type.Name : $"the registration at position {identity}";

        /// <summary>
        /// FR-17 — Warning, conflicting repeated opt-in. More than one distinct <see cref="ScopeAffinity"/>
        /// value is carried by a registered <see cref="ScopeAffinityOverride"/> instance (D18: the
        /// last-registered one is effective, matching <see cref="DuplicateScopeProvider"/>'s own
        /// last-registration-wins rule for the provider, so both halves of a repeated
        /// <c>AddBrighterRequestScope</c> gesture resolve the same way). Distinctness is judged over the
        /// affinity <b>value</b>, read from each unkeyed descriptor's constructed instance — never a
        /// registration-position fallback, which has no defensible "unknown, treat as distinct" reading
        /// for a value and would falsely report the idempotent identical-affinity repeat this rule must
        /// exclude. A descriptor with no readable instance (registered by factory delegate, FR-17's other
        /// rule) contributes nothing to the distinctness set.
        /// </summary>
        /// <returns>A simple specification reporting a Warning naming every distinct affinity registered,
        /// identifying the last-registered one as effective, and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> RepeatedOptIn()
            => new Specification<ScopeConfiguration>(
                c => UnkeyedOverrideAffinities(c).Distinct().Count() <= 1,
                c =>
                {
                    var affinities = UnkeyedOverrideAffinities(c).ToList();
                    var distinctAffinities = affinities.Distinct().ToList();
                    var effective = affinities.Last();
                    return new ValidationError(
                        ValidationSeverity.Warning,
                        "Brighter options",
                        $"AddBrighterRequestScope was called more than once with different affinities " +
                        $"({string.Join(", ", distinctAffinities)}) — the last call's affinity, {effective}, is " +
                        "effective and the others are silently discarded. Call AddBrighterRequestScope only " +
                        "once. See docs/guides/lifetimes-and-scoping.md for guidance.");
                });

        private static IEnumerable<ScopeAffinity> UnkeyedOverrideAffinities(ScopeConfiguration configuration)
            => configuration.AffinityOverrideRegistrations
                .Where(d => d.ServiceKey is null)
                .Select(d => (d.ImplementationInstance as ScopeAffinityOverride)?.Affinity)
                .Where(affinity => affinity.HasValue)
                .Select(affinity => affinity!.Value);

        /// <summary>
        /// FR-17 — Warning, unreadable override. An affinity-override descriptor supplies neither a
        /// statically known <see cref="DescriptorRecord.ImplementationType"/> nor a
        /// <see cref="DescriptorRecord.ImplementationInstance"/> — the shape a registration by factory
        /// delegate takes, since a <c>ServiceDescriptor</c> is always exactly one of implementation type,
        /// instance or factory. The override still takes effect (D18's write-through resolves it), but
        /// <see cref="RepeatedOptIn"/> cannot read a value off it, so a conflicting repeat carrying it goes
        /// unreported — this rule reports the registration shape itself, which is readable, instead.
        /// </summary>
        /// <returns>A simple specification reporting a Warning stating that an affinity override is
        /// registered by factory delegate, that its value cannot be read without resolving it, the remedy,
        /// and the guidance page.</returns>
        public static ISpecification<ScopeConfiguration> UnreadableOverride()
            => new Specification<ScopeConfiguration>(
                c => !HasUnreadableOverride(c),
                _ => new ValidationError(
                    ValidationSeverity.Warning,
                    "Scope affinity registration",
                    "An affinity override is registered by factory delegate — its value cannot be read " +
                    "without resolving it, so a conflicting repeat carrying it cannot be reported. Register " +
                    "the override as a constructed instance instead. " +
                    "See docs/guides/lifetimes-and-scoping.md for guidance."));

        private static bool HasUnreadableOverride(ScopeConfiguration configuration)
            => configuration.AffinityOverrideRegistrations
                .Where(d => d.ServiceKey is null)
                .Any(d => d.ImplementationType is null && d.ImplementationInstance is null);

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
