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

namespace Paramore.Brighter.Validation
{
    /// <summary>
    /// The base asynchronous validation pipeline handler, the counterpart of
    /// <see cref="ValidateRequestHandler{TRequest}"/>. A derived, provider-specific handler supplies the
    /// failures via <see cref="ValidateAsync"/>; if there are any, this handler throws a
    /// <see cref="RequestValidationException"/> instead of calling the next handler. Attach it to a handler
    /// with <see cref="ValidateQueryAsyncAttribute"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being validated.</typeparam>
    public abstract class ValidateRequestHandlerAsync<TRequest> : RequestHandlerAsync<TRequest>
        where TRequest : class, IRequest
    {
        /// <summary>
        /// Validates <paramref name="command"/> and, when valid, passes it to the next handler in the pipeline.
        /// </summary>
        /// <param name="command">The request to validate.</param>
        /// <param name="cancellationToken">Allows the caller to cancel validation and the rest of the pipeline.</param>
        /// <returns>The request, after the rest of the pipeline has run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
        /// <exception cref="RequestValidationException">Thrown when the request is invalid.</exception>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            IReadOnlyCollection<ValidationError> errors =
                await ValidateAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
            if (errors.Count > 0)
                throw new RequestValidationException($"Validation failed for {typeof(TRequest).Name}.", errors);

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }

        /// <summary>
        /// Runs the configured validator against <paramref name="request"/> and returns the failures.
        /// </summary>
        /// <param name="request">The request to validate.</param>
        /// <param name="cancellationToken">Allows the caller to cancel validation.</param>
        /// <returns>The validation failures, or an empty collection if the request is valid.</returns>
        protected abstract Task<IReadOnlyCollection<ValidationError>> ValidateAsync(TRequest request, CancellationToken cancellationToken);
    }
}
