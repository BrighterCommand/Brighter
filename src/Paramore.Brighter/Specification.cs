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
namespace Paramore.Brighter;

/// <summary>
/// Specification pattern interface supporting both predicate evaluation and visitor-based
/// validation result collection.
/// </summary>
/// <typeparam name="TData">The entity type to evaluate.</typeparam>
public interface ISpecification<TData>
{
    /// <summary>Evaluates whether the entity satisfies this specification.</summary>
    bool IsSatisfiedBy(TData entity);

    /// <summary>
    /// Accepts a visitor that can traverse the specification graph. Calling code
    /// invokes this after IsSatisfiedBy returns false to collect detailed results.
    /// </summary>
    TResult Accept<TResult>(ISpecificationVisitor<TData, TResult> visitor);

    /// <summary>Composes this specification with another using logical AND.</summary>
    ISpecification<TData> And(ISpecification<TData> other);

    /// <summary>Composes this specification with another using logical OR.</summary>
    ISpecification<TData> Or(ISpecification<TData> other);

    /// <summary>Negates this specification.</summary>
    ISpecification<TData> Not();

    /// <summary>Negates this specification with a validation error factory for reporting.</summary>
    /// <param name="errorFactory">Produces a ValidationError when negation fails.</param>
    ISpecification<TData> Not(Func<TData, ValidationError> errorFactory);

    /// <summary>Composes this specification with the negation of another using logical AND.</summary>
    ISpecification<TData> AndNot(ISpecification<TData> other);

    /// <summary>Composes this specification with the negation of another using logical OR.</summary>
    ISpecification<TData> OrNot(ISpecification<TData> other);
}

/// <summary>
/// A specification that evaluates an entity against a predicate expression and optionally
/// carries validation metadata for reporting via the visitor pattern.
/// </summary>
/// <typeparam name="T">The entity type to evaluate.</typeparam>
public class Specification<T> : ISpecification<T>
{
    private readonly Func<T, bool> _expression;
    private readonly Func<T, ValidationError>? _errorFactory;
    private readonly Func<T, IEnumerable<ValidationResult>>? _resultEvaluator;
    private IReadOnlyList<ValidationResult> _lastResults = [];

    /// <summary>
    /// Pure predicate — no validation metadata. Used by non-validation consumers
    /// (e.g. ExclusiveChoice in Mediator workflows).
    /// </summary>
    /// <param name="expression">The predicate to evaluate.</param>
    public Specification(Func<T, bool> expression)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <summary>
    /// Simple rule: a predicate paired with an error factory. IsSatisfiedBy evaluates
    /// the predicate; on failure, stores a single ValidationResult with the error.
    /// Yields zero or one findings.
    /// </summary>
    /// <param name="expression">The predicate expressing the valid condition.</param>
    /// <param name="errorFactory">Produces a ValidationError when the predicate fails.</param>
    public Specification(Func<T, bool> expression, Func<T, ValidationError> errorFactory)
        : this(expression)
    {
        _errorFactory = errorFactory;
    }

    /// <summary>
    /// Collapsed rule: a single function that evaluates the entity and returns zero
    /// or more ValidationResults. IsSatisfiedBy derives its bool from the results —
    /// returns true only when all results indicate success (or the enumerable is empty).
    /// </summary>
    /// <param name="resultEvaluator">Evaluates the entity and returns validation results.</param>
    public Specification(Func<T, IEnumerable<ValidationResult>> resultEvaluator)
        : this(_ => true)
    {
        _resultEvaluator = resultEvaluator;
    }

    /// <summary>Evaluates whether the entity satisfies this specification.</summary>
    public bool IsSatisfiedBy(T entity)
    {
        return _resultEvaluator != null
            ? EvaluateCollapsed(entity)
            : EvaluateSimple(entity);
    }

    /// <summary>
    /// Returns the stored validation results from the most recent IsSatisfiedBy call.
    /// The visitor uses this to collect results from leaf nodes.
    /// </summary>
    internal IReadOnlyList<ValidationResult> LastResults => _lastResults;

    /// <summary>Accepts a visitor for traversing the specification graph.</summary>
    public TResult Accept<TResult>(ISpecificationVisitor<T, TResult> visitor)
        => visitor.Visit(this);

    /// <inheritdoc />
    public ISpecification<T> And(ISpecification<T> other)
        => new AndSpecification<T>(this, other);

    /// <inheritdoc />
    public ISpecification<T> Or(ISpecification<T> other)
        => new OrSpecification<T>(this, other);

    /// <inheritdoc />
    public ISpecification<T> Not()
        => new Specification<T>(x => !IsSatisfiedBy(x));

    /// <inheritdoc />
    public ISpecification<T> Not(Func<T, ValidationError> errorFactory)
        => new NotSpecification<T>(this, errorFactory);

    /// <inheritdoc />
    public ISpecification<T> AndNot(ISpecification<T> other)
        => new Specification<T>(x => IsSatisfiedBy(x) && !other.IsSatisfiedBy(x));

    /// <inheritdoc />
    public ISpecification<T> OrNot(ISpecification<T> other)
        => new Specification<T>(x => IsSatisfiedBy(x) || !other.IsSatisfiedBy(x));

    private bool EvaluateCollapsed(T entity)
    {
        try
        {
            _lastResults = _resultEvaluator!(entity).ToList();
        }
        catch (Exception ex)
        {
            _lastResults =
            [
                ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Error,
                    entity?.ToString() ?? "(unknown)",
                    $"Rule evaluation failed: {ex.Message}"))
            ];
        }

        return _lastResults.All(r => r.Success);
    }

    private bool EvaluateSimple(T entity)
    {
        bool satisfied;
        try
        {
            satisfied = _expression(entity);
        }
        catch (Exception ex)
        {
            _lastResults =
            [
                ValidationResult.Fail(new ValidationError(
                    ValidationSeverity.Error,
                    entity?.ToString() ?? "(unknown)",
                    $"Rule evaluation failed: {ex.Message}"))
            ];
            return false;
        }

        if (!satisfied && _errorFactory != null)
            _lastResults = [ValidationResult.Fail(_errorFactory(entity))];
        else
            _lastResults = [];

        return satisfied;
    }
}
