#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.Mediator;

using System;

public interface ISpecification<TData>  
{
    bool IsSatisfiedBy(TData entity);

    ISpecification<TData> And(ISpecification<TData> other);
    ISpecification<TData> Or(ISpecification<TData> other);
    ISpecification<TData> Not();
    ISpecification<TData> AndNot(ISpecification<TData> other);
    ISpecification<TData> OrNot(ISpecification<TData> other);
}

public class Specification<T>(Func<T, bool> expression) : ISpecification<T>
{
    private readonly Func<T, bool> _expression = expression ?? throw new ArgumentNullException(nameof(expression));

    public bool IsSatisfiedBy(T entity)
    {
        return _expression(entity);
    }

    public ISpecification<T> And(ISpecification<T> other)
    {
        return new Specification<T>(x => IsSatisfiedBy(x) && other.IsSatisfiedBy(x));
    }

    public ISpecification<T> Or(ISpecification<T> other)
    {
        return new Specification<T>(x => IsSatisfiedBy(x) || other.IsSatisfiedBy(x));
    }

    public ISpecification<T> Not()
    {
        return new Specification<T>(x => !IsSatisfiedBy(x));
    }

    public ISpecification<T> AndNot(ISpecification<T> other)
    {
        return new Specification<T>(x => IsSatisfiedBy(x) && !other.IsSatisfiedBy(x));
    }

    public ISpecification<T> OrNot(ISpecification<T> other)
    {
        return new Specification<T>(x => IsSatisfiedBy(x) || !other.IsSatisfiedBy(x));
    }
}
