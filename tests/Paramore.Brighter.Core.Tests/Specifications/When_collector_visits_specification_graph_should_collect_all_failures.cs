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

using System.Linq;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Specifications;

public class ValidationResultCollectorGraphTests
{
    [Fact]
    public void When_ValidationResult_Ok_should_have_success_true_and_no_error()
    {
        // Act
        var result = ValidationResult.Ok();

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void When_ValidationResult_Fail_should_carry_the_error()
    {
        // Arrange
        var error = new ValidationError(ValidationSeverity.Warning, "MySource", "something wrong");

        // Act
        var result = ValidationResult.Fail(error);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(ValidationSeverity.Warning, result.Error.Severity);
        Assert.Equal("MySource", result.Error.Source);
        Assert.Equal("something wrong", result.Error.Message);
    }

    [Fact]
    public void When_collector_visits_or_specification_graph_should_collect_from_both_children()
    {
        // Arrange — Or: both children fail, collector should collect from both
        var left = new Specification<int>(
            n => n > 100,
            n => new ValidationError(ValidationSeverity.Error, "Left", "too small"));
        var right = new Specification<int>(
            n => n < 0,
            n => new ValidationError(ValidationSeverity.Error, "Right", "not negative"));

        var combined = left.Or(right);
        var collector = new ValidationResultCollector<int>();

        // Act — 5 fails both (not > 100, not < 0)
        var satisfied = combined.IsSatisfiedBy(5);
        var results = combined.Accept(collector).ToList();

        // Assert
        Assert.False(satisfied);
        var failures = results.Where(r => !r.Success).ToList();
        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, r => r.Error?.Source == "Left");
        Assert.Contains(failures, r => r.Error?.Source == "Right");
    }

    [Fact]
    public void When_collector_visits_not_specification_with_error_should_collect_negation_error()
    {
        // Arrange — Not with error factory: inner passes, so Not fails
        var inner = new Specification<string>(
            s => s.Length > 2,
            s => new ValidationError(ValidationSeverity.Error, "Inner", "too short"));

        var negated = inner.Not(
            s => new ValidationError(ValidationSeverity.Error, "Negation", "should be short but was long"));
        var collector = new ValidationResultCollector<string>();

        // Act — "hello" satisfies inner (length > 2), so Not fails
        var satisfied = negated.IsSatisfiedBy("hello");
        var results = negated.Accept(collector).ToList();

        // Assert
        Assert.False(satisfied);
        Assert.Single(results);
        Assert.Equal("Negation", results[0].Error!.Source);
    }

    [Fact]
    public void When_collector_visits_nested_and_or_graph_should_collect_all_leaf_failures()
    {
        // Arrange — (A AND B) OR C, where A and C fail, B passes
        var specA = new Specification<int>(
            n => n > 10,
            n => new ValidationError(ValidationSeverity.Error, "A", "not > 10"));
        var specB = new Specification<int>(
            n => n > 0,
            n => new ValidationError(ValidationSeverity.Error, "B", "not > 0"));
        var specC = new Specification<int>(
            n => n < 0,
            n => new ValidationError(ValidationSeverity.Error, "C", "not < 0"));

        // (A AND B) OR C
        var combined = specA.And(specB).Or(specC);
        var collector = new ValidationResultCollector<int>();

        // Act — 5: A fails (not > 10), B passes (> 0), C fails (not < 0)
        var satisfied = combined.IsSatisfiedBy(5);
        var results = combined.Accept(collector).ToList();

        // Assert — collector traverses full tree, finds A and C failures
        Assert.False(satisfied);
        var failures = results.Where(r => !r.Success).ToList();
        Assert.Equal(2, failures.Count);
        Assert.Contains(failures, r => r.Error?.Source == "A");
        Assert.Contains(failures, r => r.Error?.Source == "C");
    }
}
