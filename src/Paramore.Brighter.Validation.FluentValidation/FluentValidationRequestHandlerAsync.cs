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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Validation.FluentValidation
{
    /// <summary>
    /// The FluentValidation implementation of the asynchronous validation handler. It resolves a
    /// FluentValidation <c>IValidator&lt;TRequest&gt;</c> from the container and awaits its failures, which the
    /// base <see cref="ValidateRequestHandlerAsync{TRequest}"/> turns into a
    /// <see cref="RequestValidationException"/> when the request is invalid. Register it with
    /// <see cref="FluentValidationBuilderExtensions.UseFluentValidation"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being validated.</typeparam>
    /// <remarks>
    /// The pipeline's <see cref="CancellationToken"/> is passed to <c>IValidator&lt;TRequest&gt;.ValidateAsync</c>,
    /// so a cancelled token cancels validation. The handler holds no per-request state, so it is safe to reuse
    /// across concurrent pipelines.
    /// </remarks>
    public class FluentValidationRequestHandlerAsync<TRequest>(IServiceProvider serviceProvider) : ValidateRequestHandlerAsync<TRequest>
        where TRequest : class, IRequest
    {
        /// <inheritdoc />
        /// <exception cref="ConfigurationException">Thrown when no <c>IValidator&lt;TRequest&gt;</c> is registered.</exception>
        protected override async Task<IReadOnlyCollection<ValidationError>> ValidateAsync(TRequest request, CancellationToken cancellationToken)
        {
            var validator = (global::FluentValidation.IValidator<TRequest>?)serviceProvider.GetService(typeof(global::FluentValidation.IValidator<TRequest>));
            if (validator is null)
                throw new ConfigurationException(FluentValidationErrors.NoValidatorRegistered(typeof(TRequest)));

            global::FluentValidation.Results.ValidationResult result =
                await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            return FluentValidationErrors.ToErrors(result);
        }
    }
}
