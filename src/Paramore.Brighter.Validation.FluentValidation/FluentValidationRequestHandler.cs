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

namespace Paramore.Brighter.Validation.FluentValidation;

/// <summary>
/// The FluentValidation implementation of the synchronous validation handler. It resolves a
/// FluentValidation <c>IValidator&lt;TRequest&gt;</c> from the container and returns its failures to the
/// base <see cref="ValidateRequestHandler{TRequest}"/>, which throws a
/// <see cref="RequestValidationException"/> when the request is invalid. Register it with
/// <see cref="FluentValidationBuilderExtensions.UseFluentValidation"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request being validated.</typeparam>
/// <remarks>
/// The validator is resolved through <see cref="IServiceProvider"/> on each call so a missing registration
/// can be reported as a <see cref="ConfigurationException"/>. The handler holds no per-request state, so it
/// is safe to reuse across concurrent pipelines.
/// </remarks>
public class FluentValidationRequestHandler<TRequest>(IServiceProvider serviceProvider) : ValidateRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    /// <inheritdoc />
    /// <exception cref="ConfigurationException">Thrown when no <c>IValidator&lt;TRequest&gt;</c> is registered.</exception>
    protected override IReadOnlyCollection<RequestValidationError> Validate(TRequest request)
    {
        var validator = (global::FluentValidation.IValidator<TRequest>?)serviceProvider.GetService(typeof(global::FluentValidation.IValidator<TRequest>));
        if (validator is null)
            throw new ConfigurationException(FluentValidationErrors.NoValidatorRegistered(typeof(TRequest)));

        global::FluentValidation.Results.ValidationResult result = validator.Validate(request);
        return FluentValidationErrors.ToErrors(result);
    }
}

/// <summary>
/// Shared message and mapping helpers for the FluentValidation handlers.
/// </summary>
internal static class FluentValidationErrors
{
    public static string NoValidatorRegistered(Type requestType)
        => $"No FluentValidation IValidator<{requestType.Name}> is registered. Register a validator for {requestType.Name} (for example services.AddScoped<IValidator<{requestType.Name}>, {requestType.Name}Validator>()) or remove the [ValidateRequest] attribute from its handler.";

    public static IReadOnlyCollection<RequestValidationError> ToErrors(global::FluentValidation.Results.ValidationResult result)
    {
        if (result.IsValid)
            return System.Array.Empty<RequestValidationError>();

        var errors = new List<RequestValidationError>(result.Errors.Count);
        foreach (var failure in result.Errors)
        {
            errors.Add(new RequestValidationError(
                failure.PropertyName,
                failure.ErrorMessage,
                failure.AttemptedValue,
                failure.ErrorCode));
        }

        return errors;
    }
}
