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

public class AndSpecificationTests
{
    [Fact]
    public void When_both_sides_fail_should_collect_errors_from_both()
    {
        // Arrange — two simple rules that both fail for short strings
        var left = new Specification<string>(
            s => s.Length > 5,
            s => new ValidationError(ValidationSeverity.Error, "Left", "too short"));
        var right = new Specification<string>(
            s => s.StartsWith("X"),
            s => new ValidationError(ValidationSeverity.Error, "Right", "wrong prefix"));

        var combined = left.And(right);
        var collector = new ValidationResultCollector<string>();

        // Act
        var satisfied = combined.IsSatisfiedBy("abc");
        var results = combined.Accept(collector).ToList();

        // Assert — both sides evaluated, both errors collected
        Assert.False(satisfied);
        Assert.Equal(2, results.Count(r => !r.Success));
        Assert.Contains(results, r => r.Error?.Source == "Left");
        Assert.Contains(results, r => r.Error?.Source == "Right");
    }

    [Fact]
    public void When_left_fails_right_passes_should_collect_only_left_error()
    {
        // Arrange
        var left = new Specification<string>(
            s => s.Length > 5,
            s => new ValidationError(ValidationSeverity.Error, "Left", "too short"));
        var right = new Specification<string>(
            s => s.StartsWith("a"),
            s => new ValidationError(ValidationSeverity.Error, "Right", "wrong prefix"));

        var combined = left.And(right);
        var collector = new ValidationResultCollector<string>();

        // Act — "abc" fails left (length <= 5) but passes right (starts with 'a')
        var satisfied = combined.IsSatisfiedBy("abc");
        var results = combined.Accept(collector).ToList();

        // Assert
        Assert.False(satisfied);
        var failures = results.Where(r => !r.Success).ToList();
        Assert.Single(failures);
        Assert.Equal("Left", failures[0].Error!.Source);
    }

    [Fact]
    public void When_both_sides_pass_should_return_true()
    {
        // Arrange
        var left = new Specification<string>(
            s => s.Length > 2,
            s => new ValidationError(ValidationSeverity.Error, "Left", "too short"));
        var right = new Specification<string>(
            s => s.StartsWith("h"),
            s => new ValidationError(ValidationSeverity.Error, "Right", "wrong prefix"));

        var combined = left.And(right);
        var collector = new ValidationResultCollector<string>();

        // Act
        var satisfied = combined.IsSatisfiedBy("hello");
        var results = combined.Accept(collector).ToList();

        // Assert
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
