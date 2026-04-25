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

namespace Paramore.Brighter;

/// <summary>
/// Negates an inner specification. When negation fails (inner succeeded), stores its
/// own validation error provided via the error factory constructor.
/// </summary>
/// <typeparam name="T">The entity type to evaluate.</typeparam>
public class NotSpecification<T> : ISpecification<T>
{
    private readonly ISpecification<T> _inner;
    private readonly Func<T, ValidationError>? _errorFactory;
    private IReadOnlyList<ValidationResult> _lastResults = [];

    /// <summary>
    /// Creates a Not specification with an error factory for validation reporting.
    /// When the inner spec succeeds (making Not fail), the error factory provides
    /// the validation error since the inner spec has no stored errors.
    /// </summary>
    /// <param name="inner">The specification to negate.</param>
    /// <param name="errorFactory">Produces a ValidationError when negation fails.</param>
    public NotSpecification(ISpecification<T> inner, Func<T, ValidationError> errorFactory)
    {
        _inner = inner;
        _errorFactory = errorFactory;
    }

    /// <summary>Returns true when the inner specification is NOT satisfied.</summary>
    public bool IsSatisfiedBy(T entity)
    {
        var innerResult = _inner.IsSatisfiedBy(entity);
        if (innerResult && _errorFactory != null)
            _lastResults = [ValidationResult.Fail(_errorFactory(entity))];
        else
            _lastResults = [];

        return !innerResult;
    }

    /// <summary>
    /// Returns the stored validation results from the most recent IsSatisfiedBy call.
    /// </summary>
    internal IReadOnlyList<ValidationResult> LastResults => _lastResults;

    /// <inheritdoc />
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
        => new AndSpecification<T>(this, new Specification<T>(x => !other.IsSatisfiedBy(x)));

    /// <inheritdoc />
    public ISpecification<T> OrNot(ISpecification<T> other)
        => new OrSpecification<T>(this, new Specification<T>(x => !other.IsSatisfiedBy(x)));
}
