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
    /// Thrown by a <see cref="ValidateRequestHandler{TRequest}"/> or
    /// <see cref="ValidateRequestHandlerAsync{TRequest}"/> when a request fails validation. It carries the
    /// individual <see cref="ValidationError"/> failures so a caller (for example an API edge that maps to a
    /// 422 response) can report exactly which fields were invalid, regardless of the validation provider used.
    /// </summary>
    public class RequestValidationException : Exception
    {
        private static readonly IReadOnlyCollection<ValidationError> s_noErrors = new List<ValidationError>(0);

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestValidationException"/> class with no errors.
        /// </summary>
        public RequestValidationException()
            : this("Request validation failed", s_noErrors)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestValidationException"/> class with a message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public RequestValidationException(string message)
            : this(message, s_noErrors)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestValidationException"/> class with a message and the structured failures.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errors">The set of <see cref="ValidationError"/> that caused the failure.</param>
        public RequestValidationException(string message, IReadOnlyCollection<ValidationError> errors)
            : base(message)
        {
            Errors = errors ?? s_noErrors;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestValidationException"/> class with a message, the structured failures and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="errors">The set of <see cref="ValidationError"/> that caused the failure.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public RequestValidationException(string message, IReadOnlyCollection<ValidationError> errors, Exception innerException)
            : base(message, innerException)
        {
            Errors = errors ?? s_noErrors;
        }

        /// <summary>
        /// Gets the validation failures that caused this exception.
        /// </summary>
        /// <value>A read-only collection of <see cref="ValidationError"/>; empty if none were supplied.</value>
        public IReadOnlyCollection<ValidationError> Errors { get; }
    }
}
