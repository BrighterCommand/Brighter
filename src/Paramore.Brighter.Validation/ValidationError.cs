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

namespace Paramore.Brighter.Validation
{
    /// <summary>
    /// A single, framework-agnostic validation failure carried by <see cref="RequestValidationException"/>.
    /// A validation provider (FluentValidation, DataAnnotations, the Specification pattern, ...) maps its own
    /// failures onto this type so callers can read them without depending on the provider.
    /// </summary>
    /// <param name="PropertyName">The name of the request property that failed validation.</param>
    /// <param name="ErrorMessage">The human-readable description of why validation failed.</param>
    /// <param name="AttemptedValue">The value that was supplied for <paramref name="PropertyName"/>, if available.</param>
    /// <param name="ErrorCode">The provider-defined error code for the failure, if any.</param>
    public sealed record ValidationError(
        string PropertyName,
        string ErrorMessage,
        object? AttemptedValue = null,
        string? ErrorCode = null);
}
