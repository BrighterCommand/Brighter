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
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.DependencyInjection
{
    /// <summary>
    /// One host's scope configuration, exactly as the container-backed factories see it (ADR 0074 step 2) -
    /// the affinity and the three configured lifetimes read from the resolved <see cref="IBrighterOptions"/>,
    /// and three lists of <see cref="DescriptorRecord"/>, each in registration order, read from the service
    /// collection at <c>ValidatePipelines()</c> call time. Six of the seven scope-configuration rules
    /// evaluate this type.
    /// </summary>
    /// <remarks>
    /// A record rather than a parameter list of three same-typed <see cref="ServiceLifetime"/> values,
    /// which is exactly the transposition hazard <c>ValidationProviderRegistrations</c> was introduced to
    /// avoid (ADR 0074, <c>Technology Choices</c>).
    /// </remarks>
    /// <param name="Affinity">The affinity the factories read - <see cref="IBrighterOptions.DefaultScopeAffinity"/>.</param>
    /// <param name="HandlerLifetime">The configured handler lifetime - <see cref="IBrighterOptions.HandlerLifetime"/>.</param>
    /// <param name="MapperLifetime">The configured mapper lifetime - <see cref="IBrighterOptions.MapperLifetime"/>.</param>
    /// <param name="TransformerLifetime">The configured transformer lifetime - <see cref="IBrighterOptions.TransformerLifetime"/>.</param>
    /// <param name="ScopeProviderRegistrations">Every <see cref="IAmAScopeProvider"/> descriptor, in
    /// registration order (FR-24.3).</param>
    /// <param name="AffinityOverrideRegistrations">Every <see cref="ScopeAffinityOverride"/> descriptor, in
    /// registration order (FR-17).</param>
    /// <param name="BrighterOptionsRegistrations">Every <see cref="IBrighterOptions"/> descriptor, in
    /// registration order (FR-22.4).</param>
    internal sealed record ScopeConfiguration(
        ScopeAffinity Affinity,
        ServiceLifetime HandlerLifetime,
        ServiceLifetime MapperLifetime,
        ServiceLifetime TransformerLifetime,
        IReadOnlyList<DescriptorRecord> ScopeProviderRegistrations,
        IReadOnlyList<DescriptorRecord> AffinityOverrideRegistrations,
        IReadOnlyList<DescriptorRecord> BrighterOptionsRegistrations);
}
