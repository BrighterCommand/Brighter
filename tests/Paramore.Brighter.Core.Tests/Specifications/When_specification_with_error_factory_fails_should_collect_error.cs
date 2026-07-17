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
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter.Core.Tests.Specifications;
public class SpecificationValidationTests
{
    [Test]
    public async Task When_pure_predicate_fails_should_return_empty_results_from_visitor()
    {
        // Arrange — pure predicate constructor, no validation metadata
        var spec = new Specification<string>(s => s.Length > 5);
        var collector = new ValidationResultCollector<string>();
        // Act
        var satisfied = spec.IsSatisfiedBy("abc");
        var results = spec.Accept(collector).ToList();
        // Assert
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_simple_rule_fails_should_collect_validation_error()
    {
        // Arrange — simple rule: predicate + error factory
        var spec = new Specification<string>(s => s.Length > 5, s => new ValidationError(ValidationSeverity.Error, "TestSource", $"String '{s}' is too short"));
        var collector = new ValidationResultCollector<string>();
        // Act
        var satisfied = spec.IsSatisfiedBy("abc");
        var results = spec.Accept(collector).ToList();
        // Assert
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Success).IsFalse();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Error);
        await Assert.That(results[0].Error!.Source).IsEqualTo("TestSource");
        await Assert.That(results[0].Error!.Message).Contains("abc");
    }

    [Test]
    public async Task When_simple_rule_passes_should_return_empty_results()
    {
        // Arrange
        var spec = new Specification<string>(s => s.Length > 2, s => new ValidationError(ValidationSeverity.Error, "TestSource", "too short"));
        var collector = new ValidationResultCollector<string>();
        // Act
        var satisfied = spec.IsSatisfiedBy("hello");
        var results = spec.Accept(collector).ToList();
        // Assert
        await Assert.That(satisfied).IsTrue();
        await Assert.That(results).IsEmpty();
    }

    [Test]
    public async Task When_collapsed_rule_returns_failures_should_collect_all()
    {
        // Arrange — collapsed rule: evaluator returns multiple results
        var spec = new Specification<int>(n => new List<ValidationResult> { ValidationResult.Fail(new ValidationError(ValidationSeverity.Error, "Item1", "first error")), ValidationResult.Ok(), ValidationResult.Fail(new ValidationError(ValidationSeverity.Warning, "Item3", "second error")) });
        var collector = new ValidationResultCollector<int>();
        // Act
        var satisfied = spec.IsSatisfiedBy(42);
        var results = spec.Accept(collector).ToList();
        // Assert — IsSatisfiedBy returns false because not all results are successful
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Success).IsFalse();
        await Assert.That(results[1].Success).IsTrue();
        await Assert.That(results[2].Success).IsFalse();
        await Assert.That(results[2].Error!.Severity).IsEqualTo(ValidationSeverity.Warning);
    }

    [Test]
    public async Task When_predicate_throws_should_produce_error_severity_result()
    {
        // Arrange — predicate that throws an exception
        var spec = new Specification<string>(s => throw new InvalidOperationException("boom"), s => new ValidationError(ValidationSeverity.Warning, "Test", "unused"));
        var collector = new ValidationResultCollector<string>();
        // Act
        var satisfied = spec.IsSatisfiedBy("test");
        var results = spec.Accept(collector).ToList();
        // Assert — exception is caught, produces Error-severity result
        await Assert.That(satisfied).IsFalse();
        await Assert.That(results).HasSingleItem();
        await Assert.That(results[0].Success).IsFalse();
        await Assert.That(results[0].Error!.Severity).IsEqualTo(ValidationSeverity.Error);
        await Assert.That(results[0].Error!.Message).Contains("boom");
    }

    [Test]
    public async Task When_visitor_called_without_re_evaluating_should_return_cached_results()
    {
        // Arrange — track evaluation count via closure
        var evaluationCount = 0;
        var spec = new Specification<string>(s =>
        {
            evaluationCount++;
            return s.Length > 5;
        }, s => new ValidationError(ValidationSeverity.Error, "Test", "too short"));
        var collector = new ValidationResultCollector<string>();
        // Act — evaluate once, then visit twice
        spec.IsSatisfiedBy("abc");
        var results1 = spec.Accept(collector).ToList();
        var results2 = spec.Accept(collector).ToList();
        // Assert — predicate evaluated only once; visitor reads cached results
        await Assert.That(evaluationCount).IsEqualTo(1);
        await Assert.That(results1).HasSingleItem();
        await Assert.That(results2).HasSingleItem();
    }
}