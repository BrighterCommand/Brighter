#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.Extensions.DependencyInjection;

/// <summary>
/// The default <see cref="IAmATransformerResolvabilityProbe"/>. Answers whether a transformer type can be
/// resolved by testing membership against the service types registered in the <see cref="IServiceCollection"/>
/// at construction time — Brighter registers transforms as their own service type (<c>ServiceType == transform</c>),
/// so a transformer is resolvable exactly when a descriptor for that type exists. The set of registered service
/// types is snapshotted on construction; the probe never resolves the container and never instantiates a transformer.
/// </summary>
public sealed class ServiceCollectionTransformerResolvabilityProbe : IAmATransformerResolvabilityProbe
{
    private readonly HashSet<Type> _registeredServiceTypes;

    /// <summary>
    /// Initializes a new instance that snapshots the service types currently registered in <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection whose registered service types are probed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public ServiceCollectionTransformerResolvabilityProbe(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        _registeredServiceTypes = new HashSet<Type>(services.Select(descriptor => descriptor.ServiceType));
    }

    /// <inheritdoc />
    public bool Resolves(Type transformerType) => _registeredServiceTypes.Contains(transformerType);
}
