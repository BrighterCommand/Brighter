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

namespace Paramore.Brighter;

/// <summary>
/// Composes two specifications with logical OR, evaluating both sides unconditionally
/// so the visitor can collect errors from both children on failure.
/// </summary>
/// <typeparam name="T">The entity type to evaluate.</typeparam>
public class OrSpecification<T>(ISpecification<T> left, ISpecification<T> right) : ISpecification<T>
{
    /// <summary>The left child specification.</summary>
    public ISpecification<T> Left { get; } = left;

    /// <summary>The right child specification.</summary>
    public ISpecification<T> Right { get; } = right;

    /// <summary>
    /// Evaluates both children unconditionally and returns true when either is satisfied.
    /// </summary>
    public bool IsSatisfiedBy(T entity)
    {
        var l = Left.IsSatisfiedBy(entity);
        var r = Right.IsSatisfiedBy(entity);
        return l || r;
    }

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
