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

using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Aggregates validation findings (errors and warnings) from pipeline validation.
/// Errors prevent startup; warnings are logged but do not prevent startup.
/// </summary>
/// <param name="errors">Validation errors (severity = Error).</param>
/// <param name="warnings">Validation warnings (severity = Warning).</param>
public sealed class PipelineValidationResult(IEnumerable<ValidationError> errors, IEnumerable<ValidationError> warnings)
{
    /// <summary>Validation errors that prevent startup.</summary>
    public IReadOnlyList<ValidationError> Errors { get; } = errors.ToList().AsReadOnly();

    /// <summary>Validation warnings that are logged but do not prevent startup.</summary>
    public IReadOnlyList<ValidationError> Warnings { get; } = warnings.ToList().AsReadOnly();

    /// <summary>True when there are no errors. Warnings alone do not make the result invalid.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Throws <see cref="PipelineValidationException"/> if this result contains any errors.
    /// Does nothing if the result is valid (warnings only or empty).
    /// </summary>
    /// <exception cref="PipelineValidationException">Thrown when <see cref="IsValid"/> is false.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new PipelineValidationException(this);
    }

    /// <summary>
    /// Combines multiple <see cref="PipelineValidationResult"/> instances into a single result,
    /// merging all errors and warnings.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A new <see cref="PipelineValidationResult"/> containing all errors and warnings from the inputs.</returns>
    public static PipelineValidationResult Combine(params PipelineValidationResult[] results)
    {
        var allErrors = results.SelectMany(r => r.Errors);
        var allWarnings = results.SelectMany(r => r.Warnings);
        return new PipelineValidationResult(allErrors, allWarnings);
    }
}
