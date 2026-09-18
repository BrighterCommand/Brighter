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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// The <see cref="ServiceDescriptor"/>s of an <see cref="IServiceCollection"/> as they stood when this
    /// snapshot was built (ADR 0074 step 2) - taken at <c>ValidatePipelines()</c> call time, matching
    /// <c>ValidationProviderRegistrations</c> and <c>ServiceCollectionTransformerResolvabilityProbe</c>'s
    /// own capture point. Answers three queries without resolving anything and without instantiating a
    /// single artefact; together they are everything the scope-configuration rules read from the collection.
    /// </summary>
    /// <remarks>
    /// Constructed by <c>BrighterPipelineValidationExtensions</c> at <c>ValidatePipelines()</c> call time
    /// and read by every rule in <c>ScopeConfigurationRules</c>.
    /// </remarks>
    internal sealed class ContainerRegistrationSnapshot
    {
        private readonly List<ServiceDescriptor> _descriptors;

        /// <summary>
        /// Snapshots <paramref name="services"/> as it stands at construction time.
        /// </summary>
        /// <param name="services">The service collection to snapshot.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public ContainerRegistrationSnapshot(IServiceCollection services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            _descriptors = new List<ServiceDescriptor>(services);
        }

        /// <summary>
        /// The effective lifetime for <paramref name="serviceType"/> - the last <b>unkeyed</b> descriptor,
        /// matching Microsoft's own resolution, or, when <paramref name="serviceKey"/> is supplied, the
        /// last descriptor for that <c>(type, key)</c> pair instead (FR-22.3).
        /// </summary>
        /// <param name="serviceType">The service type to look up.</param>
        /// <param name="serviceKey">The service key a parameter named with
        /// <c>[FromKeyedServices(key)]</c>, or <see langword="null"/> to read the unkeyed registration.</param>
        /// <returns>The last matching descriptor's <see cref="ServiceLifetime"/>, or <see langword="null"/>
        /// when no descriptor matches.</returns>
        public ServiceLifetime? EffectiveLifetimeOf(Type serviceType, object? serviceKey = null)
        {
            ServiceDescriptor? effective = null;
            foreach (var descriptor in _descriptors)
            {
                if (descriptor.ServiceType != serviceType) continue;

                var matchesKey = serviceKey is null
                    ? !descriptor.IsKeyedService
                    : descriptor.IsKeyedService && Equals(descriptor.ServiceKey, serviceKey);
                if (!matchesKey) continue;

                effective = descriptor;
            }
            return effective?.Lifetime;
        }

        /// <summary>
        /// Every candidate handler, mapper or transform in the snapshot, over keyed and unkeyed descriptors
        /// alike (FR-22.3) - one <see cref="ArtefactRegistration"/> per <c>(type, kind)</c> pair, so a type
        /// presenting more than one kind appears once per kind it presents. A descriptor contributes a
        /// candidate only when its implementation type is statically known; one registered by factory
        /// delegate or by instance contributes none.
        /// </summary>
        /// <param name="handlerLifetime">The configured handler lifetime a <see cref="ArtefactKind.Handler"/>
        /// candidate is recorded against - <see cref="IBrighterOptions.HandlerLifetime"/>.</param>
        /// <param name="mapperLifetime">The configured mapper lifetime a <see cref="ArtefactKind.Mapper"/>
        /// candidate is recorded against - <see cref="IBrighterOptions.MapperLifetime"/>.</param>
        /// <param name="transformerLifetime">The configured transformer lifetime a
        /// <see cref="ArtefactKind.Transform"/> candidate is recorded against - <see cref="IBrighterOptions.TransformerLifetime"/>.</param>
        /// <returns>Every candidate found, in no particular order.</returns>
        public IReadOnlyList<ArtefactRegistration> Artefacts(
            ServiceLifetime handlerLifetime, ServiceLifetime mapperLifetime, ServiceLifetime transformerLifetime)
        {
            var registrations = new List<ArtefactRegistration>();
            foreach (var descriptor in _descriptors)
            {
                var implementationType = descriptor.IsKeyedService
                    ? descriptor.KeyedImplementationType
                    : descriptor.ImplementationType;
                if (implementationType is null) continue;

                if (typeof(IHandleRequests).IsAssignableFrom(implementationType) ||
                    typeof(IHandleRequestsAsync).IsAssignableFrom(implementationType))
                    registrations.Add(new ArtefactRegistration(implementationType, ArtefactKind.Handler, handlerLifetime));

                if (typeof(IAmAMessageMapper).IsAssignableFrom(implementationType) ||
                    typeof(IAmAMessageMapperAsync).IsAssignableFrom(implementationType))
                    registrations.Add(new ArtefactRegistration(implementationType, ArtefactKind.Mapper, mapperLifetime));

                if (typeof(IAmAMessageTransform).IsAssignableFrom(implementationType) ||
                    typeof(IAmAMessageTransformAsync).IsAssignableFrom(implementationType))
                    registrations.Add(new ArtefactRegistration(implementationType, ArtefactKind.Transform, transformerLifetime));
            }
            return registrations;
        }

        /// <summary>
        /// Every descriptor registered for <paramref name="serviceType"/>, keyed and unkeyed alike, in
        /// registration order (FR-24.3, FR-17, FR-22.4).
        /// </summary>
        /// <param name="serviceType">The service type to look up.</param>
        /// <returns>One <see cref="DescriptorRecord"/> per matching descriptor, in the order each was
        /// added to the collection.</returns>
        public IReadOnlyList<DescriptorRecord> DescriptorsFor(Type serviceType)
        {
            var records = new List<DescriptorRecord>();
            foreach (var descriptor in _descriptors)
            {
                if (descriptor.ServiceType != serviceType) continue;
                records.Add(ToDescriptorRecord(descriptor, records.Count));
            }
            return records;
        }

        private DescriptorRecord ToDescriptorRecord(ServiceDescriptor descriptor, int position)
        {
            var implementationType = descriptor.IsKeyedService
                ? descriptor.KeyedImplementationType
                : descriptor.ImplementationType;
            var implementationInstance = descriptor.IsKeyedService
                ? descriptor.KeyedImplementationInstance
                : descriptor.ImplementationInstance;

            return new DescriptorRecord(
                position,
                descriptor.IsKeyedService ? descriptor.ServiceKey : null,
                implementationType,
                implementationInstance,
                IsBrighterRegistered(descriptor));
        }

        /// <summary>
        /// Whether <paramref name="descriptor"/> is the <see cref="IBrighterOptions"/> descriptor
        /// <c>RegisterBrighterOptions</c> added, per the <see cref="BrighterOptionsRegistration"/> it left
        /// in the collection alongside it (FR-22.4). Always <see langword="false"/> for any descriptor
        /// whose service type is not <see cref="IBrighterOptions"/>.
        /// </summary>
        private bool IsBrighterRegistered(ServiceDescriptor descriptor)
        {
            if (descriptor.ServiceType != typeof(IBrighterOptions)) return false;

            foreach (var candidate in _descriptors)
            {
                if (candidate.ServiceType != typeof(BrighterOptionsRegistration)) continue;
                if (!candidate.IsKeyedService &&
                    candidate.ImplementationInstance is BrighterOptionsRegistration registration &&
                    ReferenceEquals(registration.Descriptor, descriptor))
                    return true;
            }
            return false;
        }
    }
}
