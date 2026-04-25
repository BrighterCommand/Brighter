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

namespace Paramore.Brighter;

/// <summary>
/// Concrete visitor that collects all stored <see cref="ValidationResult"/> instances
/// from a specification graph.
/// </summary>
/// <typeparam name="TData">The entity type the specifications evaluate.</typeparam>
public class ValidationResultCollector<TData> : ISpecificationVisitor<TData, IEnumerable<ValidationResult>>
{
    /// <summary>Collects results from a leaf specification.</summary>
    public IEnumerable<ValidationResult> Visit(Specification<TData> specification)
        => specification.LastResults;

    /// <summary>Collects results from both children of an AND node.</summary>
    public IEnumerable<ValidationResult> Visit(AndSpecification<TData> specification)
        => specification.Left.Accept(this).Concat(specification.Right.Accept(this));

    /// <summary>Collects results from both children of an OR node.</summary>
    public IEnumerable<ValidationResult> Visit(OrSpecification<TData> specification)
        => specification.Left.Accept(this).Concat(specification.Right.Accept(this));

    /// <summary>Collects stored results from a NOT node.</summary>
    public IEnumerable<ValidationResult> Visit(NotSpecification<TData> specification)
        => specification.LastResults;
}
