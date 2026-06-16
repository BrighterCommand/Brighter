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
using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.RequestValidation.Handlers;

namespace Paramore.Brighter.Validation.Specification;

/// <summary>
/// The Specification-pattern implementation of the synchronous validation handler. It resolves an
/// <see cref="ISpecification{T}"/> for the request from the container, evaluates it, and returns the failures
/// to the base <see cref="ValidateRequestHandler{TRequest}"/>, which throws a
/// <see cref="RequestValidationException"/> when the request is invalid. Register it with
/// <see cref="SpecificationBuilderExtensions.UseSpecification"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request being validated.</typeparam>
/// <remarks>
/// The specification is resolved through <see cref="IServiceProvider"/> on each call so a missing
/// registration can be reported as a <see cref="ConfigurationException"/>. The handler itself holds no
/// per-request state. Brighter's <see cref="Specification{T}"/>, however, records the failures from the most
/// recent <c>IsSatisfiedBy</c> for the visitor to collect, so it carries per-evaluation state: register the
/// <see cref="ISpecification{T}"/> with a per-request lifetime (transient or scoped) — a single shared
/// instance is not safe to evaluate from concurrent requests.
/// </remarks>
public class SpecificationRequestHandler<TRequest>(IServiceProvider serviceProvider) : ValidateRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    /// <inheritdoc />
    /// <exception cref="ConfigurationException">Thrown when no <see cref="ISpecification{T}"/> is registered for <typeparamref name="TRequest"/>.</exception>
    protected override IReadOnlyCollection<RequestValidationError> Validate(TRequest request)
        => SpecificationErrors.Validate(request, serviceProvider);
}

/// <summary>
/// Shared resolution, evaluation and mapping helpers for the Specification handlers.
/// </summary>
internal static class SpecificationErrors
{
    public static string NoSpecificationRegistered(Type requestType)
        => $"No ISpecification<{requestType.Name}> is registered. Register a specification for {requestType.Name} (for example services.AddSingleton<ISpecification<{requestType.Name}>>(...)) or remove the [ValidateRequest] attribute from its handler.";

    public static IReadOnlyCollection<RequestValidationError> Validate<TRequest>(TRequest request, IServiceProvider serviceProvider)
        where TRequest : class
    {
        var specification = (ISpecification<TRequest>?)serviceProvider.GetService(typeof(ISpecification<TRequest>));
        if (specification is null)
            throw new ConfigurationException(NoSpecificationRegistered(typeof(TRequest)));

        if (specification.IsSatisfiedBy(request))
            return Array.Empty<RequestValidationError>();

        var results = specification.Accept(new ValidationResultCollector<TRequest>());
        return ToErrors(results, typeof(TRequest));
    }

    private static IReadOnlyCollection<RequestValidationError> ToErrors(IEnumerable<ValidationResult> results, Type requestType)
    {
        var errors = new List<RequestValidationError>();
        foreach (var result in results)
        {
            if (result.Success || result.Error is null)
                continue;

            errors.Add(new RequestValidationError(result.Error.Source, result.Error.Message));
        }

        if (errors.Count == 0)
            errors.Add(new RequestValidationError(string.Empty, $"{requestType.Name} did not satisfy its specification."));

        return errors;
    }
}
