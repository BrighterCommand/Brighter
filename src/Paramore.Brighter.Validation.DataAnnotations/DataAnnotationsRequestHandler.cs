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

namespace Paramore.Brighter.Validation.DataAnnotations;

/// <summary>
/// The <see cref="System.ComponentModel.DataAnnotations"/> implementation of the synchronous validation
/// handler. It validates the request against the data-annotation attributes declared on the request type
/// (for example <c>[Required]</c> or <c>[EmailAddress]</c>) and returns the failures to the base
/// <see cref="ValidateRequestHandler{TRequest}"/>, which throws a <see cref="RequestValidationException"/>
/// when the request is invalid. Register it with
/// <see cref="DataAnnotationsBuilderExtensions.UseDataAnnotationsValidation"/>.
/// </summary>
/// <typeparam name="TRequest">The type of the request being validated.</typeparam>
/// <remarks>
/// Unlike a validator-based provider, there is nothing to register per request — the constraints live on the
/// request type itself, so a request with no data-annotation attributes is simply valid. The
/// <see cref="IServiceProvider"/> is passed to the validation context so custom
/// <c>ValidationAttribute</c>s that resolve services continue to work. The handler holds no per-request
/// state, so it is safe to reuse across concurrent pipelines.
/// </remarks>
public class DataAnnotationsRequestHandler<TRequest>(IServiceProvider serviceProvider) : ValidateRequestHandler<TRequest>
    where TRequest : class, IRequest
{
    /// <inheritdoc />
    protected override IReadOnlyCollection<RequestValidationError> Validate(TRequest request)
        => DataAnnotationsErrors.Validate(request, serviceProvider);
}

/// <summary>
/// Shared validation and mapping helpers for the DataAnnotations handlers.
/// </summary>
internal static class DataAnnotationsErrors
{
    public static IReadOnlyCollection<RequestValidationError> Validate(object request, IServiceProvider serviceProvider)
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request, serviceProvider, items: null);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return ToErrors(results);
    }

    private static IReadOnlyCollection<RequestValidationError> ToErrors(List<System.ComponentModel.DataAnnotations.ValidationResult> results)
    {
        if (results.Count == 0)
            return Array.Empty<RequestValidationError>();

        var errors = new List<RequestValidationError>(results.Count);
        foreach (var result in results)
        {
            var message = result.ErrorMessage ?? string.Empty;

            var hasMember = false;
            foreach (var member in result.MemberNames)
            {
                hasMember = true;
                errors.Add(new RequestValidationError(member, message));
            }

            if (!hasMember)
                errors.Add(new RequestValidationError(string.Empty, message));
        }

        return errors;
    }
}
