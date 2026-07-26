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
using Paramore.Brighter.Validation;

namespace Paramore.Brighter.ServiceActivator.Validation;

/// <summary>
/// A <see cref="Specification{T}"/> that owns a disposable resource used by its evaluator (here, a
/// <see cref="MessageMapperRegistry"/> and the mapper factories it holds) and disposes it when the
/// specification itself is disposed.
/// </summary>
/// <remarks>
/// The specification is registered as a container-owned singleton, so the DI container disposes it at
/// shutdown; that cascade drains the registry's factories rather than leaving them — and, for an
/// IoC-backed factory, any scope it resolved — retained until the process exits. It subclasses
/// <see cref="Specification{T}"/> rather than decorating it so the visitor pattern
/// (<see cref="Specification{T}.Accept{TResult}"/>, which binds to the
/// <c>Visit(Specification&lt;T&gt;)</c> overload) continues to work unchanged.
/// </remarks>
/// <typeparam name="T">The entity type to evaluate.</typeparam>
internal sealed class DisposingSpecification<T> : Specification<T>, IDisposable
{
    private readonly IDisposable _owned;

    public DisposingSpecification(Func<T, IEnumerable<ValidationResult>> resultEvaluator, IDisposable owned)
        : base(resultEvaluator)
    {
        _owned = owned ?? throw new ArgumentNullException(nameof(owned));
    }

    public void Dispose() => _owned.Dispose();
}
