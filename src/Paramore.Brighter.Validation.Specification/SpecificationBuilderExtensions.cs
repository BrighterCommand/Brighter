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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.RequestValidation.Attributes;
using Paramore.Brighter.RequestValidation.Handlers;

namespace Paramore.Brighter.Validation.Specification;

/// <summary>
/// Registration extensions that add the Specification-pattern pipeline handlers to a Brighter application.
/// </summary>
public static class SpecificationBuilderExtensions
{
    /// <summary>
    /// Selects Brighter's Specification pattern as the validation provider by mapping the provider-agnostic
    /// <see cref="ValidateRequestHandler{TRequest}"/> and <see cref="ValidateRequestHandlerAsync{TRequest}"/>
    /// (the targets of <see cref="ValidateRequestAttribute"/> and <see cref="ValidateRequestAsyncAttribute"/>)
    /// to their Specification implementations.
    /// </summary>
    /// <param name="brighterBuilder">The Brighter builder to add the handlers to.</param>
    /// <returns>The same <paramref name="brighterBuilder"/>, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="brighterBuilder"/> is null.</exception>
    /// <remarks>
    /// The handlers are registered as <see cref="ServiceLifetime.Transient"/>; their effective lifetime is
    /// managed by Brighter's <c>ServiceProviderHandlerFactory</c>. You must still register an
    /// <c>ISpecification&lt;TRequest&gt;</c> for each validated request — and register it with a per-request
    /// lifetime (transient or scoped), because Brighter's <see cref="Specification{T}"/> records
    /// per-evaluation state, so a single shared instance is not safe to evaluate from concurrent requests.
    /// </remarks>
    public static IBrighterBuilder UseSpecification(this IBrighterBuilder brighterBuilder)
    {
        if (brighterBuilder is null)
            throw new ArgumentNullException(nameof(brighterBuilder));

        brighterBuilder.Services.Add(new ServiceDescriptor(typeof(ValidateRequestHandler<>), typeof(SpecificationRequestHandler<>), ServiceLifetime.Transient));
        brighterBuilder.Services.Add(new ServiceDescriptor(typeof(ValidateRequestHandlerAsync<>), typeof(SpecificationRequestHandlerAsync<>), ServiceLifetime.Transient));

        return brighterBuilder;
    }
}
