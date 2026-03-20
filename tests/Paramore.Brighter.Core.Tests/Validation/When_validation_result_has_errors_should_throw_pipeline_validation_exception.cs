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
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class PipelineValidationResultTests
{
    [Fact]
    public void When_no_errors_only_warnings_should_be_valid()
    {
        // Arrange
        var warnings = new List<ValidationError>
        {
            new(ValidationSeverity.Warning, "Handler 'OrderHandler'", "Backstop after resilience pipeline")
        };
        var errors = new List<ValidationError>();

        // Act
        var result = new PipelineValidationResult(errors, warnings);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void When_errors_present_should_not_be_valid()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new(ValidationSeverity.Error, "Handler 'OrderHandler'", "Handler type is not public")
        };
        var warnings = new List<ValidationError>();

        // Act
        var result = new PipelineValidationResult(errors, warnings);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void When_has_errors_ThrowIfInvalid_should_throw_PipelineValidationException()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new(ValidationSeverity.Error, "Handler 'OrderHandler'", "Handler type is not public"),
            new(ValidationSeverity.Error, "Producer 'OrderCreated'", "RequestType not set")
        };
        var warnings = new List<ValidationError>();
        var result = new PipelineValidationResult(errors, warnings);

        // Act
        var exception = Assert.Throws<PipelineValidationException>(() => result.ThrowIfInvalid());

        // Assert — exception is a ConfigurationException
        Assert.IsAssignableFrom<ConfigurationException>(exception);

        // Assert — exception carries the validation result
        Assert.Same(result, exception.ValidationResult);

        // Assert — message lists all errors with source context
        Assert.Contains("[Handler 'OrderHandler']", exception.Message);
        Assert.Contains("Handler type is not public", exception.Message);
        Assert.Contains("[Producer 'OrderCreated']", exception.Message);
        Assert.Contains("RequestType not set", exception.Message);
        Assert.Contains("2 error(s)", exception.Message);
    }

    [Fact]
    public void When_valid_ThrowIfInvalid_should_not_throw()
    {
        // Arrange
        var result = new PipelineValidationResult(
            new List<ValidationError>(),
            new List<ValidationError> { new(ValidationSeverity.Warning, "X", "minor") });

        // Act & Assert — no exception
        result.ThrowIfInvalid();
    }

    [Fact]
    public void When_combining_results_should_merge_errors_and_warnings()
    {
        // Arrange
        var result1 = new PipelineValidationResult(
            new List<ValidationError> { new(ValidationSeverity.Error, "A", "error from A") },
            new List<ValidationError> { new(ValidationSeverity.Warning, "A", "warning from A") });

        var result2 = new PipelineValidationResult(
            new List<ValidationError> { new(ValidationSeverity.Error, "B", "error from B") },
            new List<ValidationError>());

        var result3 = new PipelineValidationResult(
            new List<ValidationError>(),
            new List<ValidationError> { new(ValidationSeverity.Warning, "C", "warning from C") });

        // Act
        var combined = PipelineValidationResult.Combine(result1, result2, result3);

        // Assert
        Assert.False(combined.IsValid);
        Assert.Equal(2, combined.Errors.Count);
        Assert.Equal(2, combined.Warnings.Count);
        Assert.Contains(combined.Errors, e => e.Source == "A");
        Assert.Contains(combined.Errors, e => e.Source == "B");
        Assert.Contains(combined.Warnings, e => e.Source == "A");
        Assert.Contains(combined.Warnings, e => e.Source == "C");
    }
}
