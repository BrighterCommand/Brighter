#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter;

/// <summary>
/// Pairs a success/failure bool with an optional <see cref="ValidationError"/>.
/// Stored by specifications during <see cref="ISpecification{TData}.IsSatisfiedBy"/> and
/// collected by visitors for detailed reporting.
/// </summary>
public record ValidationResult
{
    /// <summary>Whether the evaluation succeeded.</summary>
    public bool Success { get; }

    /// <summary>The validation error, if any. Null when <see cref="Success"/> is true.</summary>
    public ValidationError? Error { get; }

    private ValidationResult(bool success, ValidationError? error)
    {
        Success = success;
        Error = error;
    }

    /// <summary>Creates a successful result with no error.</summary>
    public static ValidationResult Ok() => new(true, null);

    /// <summary>Creates a failed result carrying the specified error.</summary>
    /// <param name="error">The validation error describing the failure.</param>
    public static ValidationResult Fail(ValidationError error) => new(false, error);
}
