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

namespace Paramore.Brighter.Validation
{
    /// <summary>
    /// Marks the target <see cref="IHandleRequestsAsync{TRequest}.HandleAsync"/> method so that the request is
    /// validated before the business handler runs. The provider-agnostic
    /// <see cref="ValidateRequestHandlerAsync{TRequest}"/> is resolved from the container; the concrete
    /// validation framework is chosen by which provider package you register.
    /// </summary>
    /// <remarks>
    /// The asynchronous counterpart of <see cref="ValidateQueryAttribute"/>; the provider awaits its
    /// asynchronous validation and honours the pipeline's cancellation token.
    /// </remarks>
    public class ValidateQueryAsyncAttribute : RequestHandlerAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValidateQueryAsyncAttribute"/> class.
        /// </summary>
        /// <param name="step">The zero-based position of this handler within the pipeline.</param>
        /// <param name="timing">When the handler runs relative to the target. Defaults to <see cref="HandlerTiming.Before"/>, as validation must run before the business handler.</param>
        public ValidateQueryAsyncAttribute(int step, HandlerTiming timing = HandlerTiming.Before)
            : base(step, timing)
        { }

        /// <summary>
        /// Gets the type of the handler that performs the validation.
        /// </summary>
        /// <returns>The open generic <see cref="ValidateRequestHandlerAsync{TRequest}"/> type, which a provider package maps to its concrete handler.</returns>
        public override Type GetHandlerType()
        {
            return typeof(ValidateRequestHandlerAsync<>);
        }
    }
}
