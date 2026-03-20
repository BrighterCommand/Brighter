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

using System;
using System.Linq;

namespace Paramore.Brighter.Validation;

/// <summary>
/// Thrown when pipeline validation finds one or more errors.
/// Extends <see cref="ConfigurationException"/> so existing catch blocks that handle Brighter
/// configuration errors will also catch validation failures. The <see cref="ValidationResult"/>
/// is available for programmatic inspection, and the exception message includes all errors
/// with their source context.
/// </summary>
public class PipelineValidationException : ConfigurationException
{
    /// <summary>The validation result containing all errors and warnings.</summary>
    public PipelineValidationResult? ValidationResult { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineValidationException"/> class.
    /// </summary>
    public PipelineValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PipelineValidationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineValidationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PipelineValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new <see cref="PipelineValidationException"/> from the given validation result.
    /// </summary>
    /// <param name="result">The validation result containing errors.</param>
    public PipelineValidationException(PipelineValidationResult result)
        : base(FormatMessage(result))
    {
        ValidationResult = result;
    }

    private static string FormatMessage(PipelineValidationResult result)
    {
        var errorLines = result.Errors
            .Select(e => $"  [{e.Source}] {e.Message}");
        return $"Brighter pipeline validation failed with {result.Errors.Count} error(s):\n"
            + string.Join("\n", errorLines);
    }
}
