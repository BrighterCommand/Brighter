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
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// Holds the artefact types that satisfy both halves of FR-22.3's exclusion conjunction: returned by a
    /// <see cref="RequestHandlerAttribute"/> or <see cref="TransformAttribute"/> <c>GetHandlerType()</c>, and
    /// defined in an assembly whose simple name is <c>Paramore.Brighter</c> or begins <c>Paramore.Brighter.</c>
    /// (ADR 0074 step 5a).
    /// </summary>
    /// <remarks>
    /// Built from the reflection-only describe path that already exists and instantiates nothing - the
    /// handler half from <see cref="PipelineBuilder{TRequest}.Describe()"/>, which needs no publication and
    /// no subscription because it enumerates the subscriber registry itself; the transform half from
    /// <see cref="TransformPipelineBuilder.DescribeTransforms(MessageMapperRegistry, Type, bool)"/>, called
    /// with <c>includeAsync: true</c>, over every request type reachable from the publications, the
    /// subscriptions and the registered handlers. Mappers are never excluded by this mechanism - no mapper
    /// is returned by a <c>RequestHandlerAttribute</c> or <c>TransformAttribute</c>, so the conjunction
    /// cannot reach one (C-20(iv)).
    /// </remarks>
    internal static class ArtefactExclusionSet
    {
        /// <summary>
        /// Builds the exclusion set for one validation run.
        /// </summary>
        /// <param name="pipelineBuilder">Describes the handler pipelines - the handler half's source.</param>
        /// <param name="mapperRegistry">The mapper registry used to describe transform pipelines, or
        /// <see langword="null"/> when no <c>ServiceCollectionMessageMapperRegistryBuilder</c> was
        /// registered, in which case the transform half is empty.</param>
        /// <param name="publications">Publications whose request types contribute to the transform half's
        /// request-type set.</param>
        /// <param name="subscriptions">Subscriptions whose request types contribute to the transform half's
        /// request-type set.</param>
        /// <returns>Every type Brighter itself put in the pipeline.</returns>
        public static HashSet<Type> Build(
            PipelineBuilder<IRequest> pipelineBuilder,
            MessageMapperRegistry? mapperRegistry,
            IEnumerable<Publication>? publications,
            IEnumerable<Subscription>? subscriptions)
        {
            var excluded = new HashSet<Type>();
            var requestTypes = new HashSet<Type>();

            foreach (var description in pipelineBuilder.Describe())
            {
                requestTypes.Add(description.RequestType);

                foreach (var step in description.BeforeSteps.Concat(description.AfterSteps))
                {
                    if (IsBrightersOwn(step.HandlerType))
                        excluded.Add(step.HandlerType);
                }
            }

            if (mapperRegistry is not null)
            {
                foreach (var requestType in publications?.Select(p => p.RequestType) ?? [])
                {
                    if (requestType is not null) requestTypes.Add(requestType);
                }

                foreach (var requestType in subscriptions?.Select(s => s.RequestType) ?? [])
                {
                    if (requestType is not null) requestTypes.Add(requestType);
                }

                foreach (var requestType in requestTypes)
                {
                    var description = TransformPipelineBuilder.DescribeTransforms(mapperRegistry, requestType, includeAsync: true);
                    if (description is null) continue;

                    foreach (var step in description.WrapTransforms.Concat(description.UnwrapTransforms))
                    {
                        if (IsBrightersOwn(step.TransformType))
                            excluded.Add(step.TransformType);
                    }
                }
            }

            return excluded;
        }

        private static bool IsBrightersOwn(Type type)
        {
            var assemblyName = type.Assembly.GetName().Name;
            return assemblyName is not null &&
                   (assemblyName == "Paramore.Brighter" || assemblyName.StartsWith("Paramore.Brighter.", StringComparison.Ordinal));
        }
    }
}
