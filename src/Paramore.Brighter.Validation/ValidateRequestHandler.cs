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

namespace Paramore.Brighter.Validation
{
    /// <summary>
    /// The base synchronous validation pipeline handler. It runs before the business handler: a derived,
    /// provider-specific handler (for example the FluentValidation one) supplies the failures via
    /// <see cref="Validate"/>; if there are any, this handler throws a <see cref="RequestValidationException"/>
    /// instead of calling the next handler. Attach it to a handler with <see cref="ValidateQueryAttribute"/>;
    /// a provider package maps this handler to its concrete implementation in the container.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request being validated.</typeparam>
    public abstract class ValidateRequestHandler<TRequest> : RequestHandler<TRequest>
        where TRequest : class, IRequest
    {
        /// <summary>
        /// Validates <paramref name="request"/> and, when valid, passes it to the next handler in the pipeline.
        /// </summary>
        /// <param name="request">The request to validate.</param>
        /// <returns>The request, after the rest of the pipeline has run.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="RequestValidationException">Thrown when the request is invalid.</exception>
        public override TRequest Handle(TRequest request)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            IReadOnlyCollection<ValidationError> errors = Validate(request);
            if (errors.Count > 0)
                throw new RequestValidationException($"Validation failed for {typeof(TRequest).Name}.", errors);

            return base.Handle(request);
        }

        /// <summary>
        /// Runs the configured validator against <paramref name="request"/> and returns the failures.
        /// </summary>
        /// <param name="request">The request to validate.</param>
        /// <returns>The validation failures, or an empty collection if the request is valid.</returns>
        protected abstract IReadOnlyCollection<ValidationError> Validate(TRequest request);
    }
}
