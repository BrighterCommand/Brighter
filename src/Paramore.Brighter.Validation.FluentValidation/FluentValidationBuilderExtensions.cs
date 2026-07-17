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

namespace Paramore.Brighter.Validation.FluentValidation;

/// <summary>
/// Registration extensions that add the FluentValidation pipeline handlers to a Brighter application.
/// </summary>
public static class FluentValidationBuilderExtensions
{
    /// <summary>
    /// Selects FluentValidation as the validation provider by mapping the provider-agnostic
    /// <see cref="ValidateRequestHandler{TRequest}"/> and <see cref="ValidateRequestHandlerAsync{TRequest}"/>
    /// (the targets of <see cref="ValidateRequestAttribute"/> and <see cref="ValidateRequestAsyncAttribute"/>)
    /// to their FluentValidation implementations.
    /// </summary>
    /// <param name="brighterBuilder">The Brighter builder to add the handlers to.</param>
    /// <returns>The same <paramref name="brighterBuilder"/>, to allow fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="brighterBuilder"/> is null.</exception>
    /// <remarks>
    /// The handlers are registered as <see cref="ServiceLifetime.Transient"/>; their effective lifetime is
    /// managed by Brighter's <c>ServiceProviderHandlerFactory</c>. You must still register an
    /// <c>IValidator&lt;TRequest&gt;</c> for each validated request — for example with FluentValidation's
    /// <c>services.AddValidatorsFromAssembly(...)</c>.
    /// </remarks>
    public static IBrighterBuilder UseFluentValidation(this IBrighterBuilder brighterBuilder)
    {
        if (brighterBuilder is null)
            throw new ArgumentNullException(nameof(brighterBuilder));

        brighterBuilder.Services.Add(new ServiceDescriptor(typeof(ValidateRequestHandler<>), typeof(FluentValidationRequestHandler<>), ServiceLifetime.Transient));
        brighterBuilder.Services.Add(new ServiceDescriptor(typeof(ValidateRequestHandlerAsync<>), typeof(FluentValidationRequestHandlerAsync<>), ServiceLifetime.Transient));

        return brighterBuilder;
    }
}
