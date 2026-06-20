#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

using Paramore.Brighter;

namespace SpecificationSample;

/// <summary>
/// Builds the <see cref="ISpecification{T}"/> for <see cref="PlaceOrder"/> using Brighter's Specification
/// pattern: a positive-quantity rule composed with a non-empty-Sku rule via <c>And</c>, each carrying the
/// <see cref="ValidationError"/> it reports when unsatisfied. <c>And</c> evaluates both rules, so an order
/// that breaks both is reported with both errors.
/// </summary>
public static class OrderSpecification
{
    public static ISpecification<PlaceOrder> Create()
        => new Specification<PlaceOrder>(
                order => order.Quantity > 0,
                order => new ValidationError(ValidationSeverity.Error, nameof(PlaceOrder.Quantity), "Quantity must be greater than zero"))
            .And(new Specification<PlaceOrder>(
                order => !string.IsNullOrWhiteSpace(order.Sku),
                order => new ValidationError(ValidationSeverity.Error, nameof(PlaceOrder.Sku), "Sku must not be empty")));
}
