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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.Validation.Specification.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Validation.Specification.Tests;

public class InvalidSpecificationAsyncValidationTests
{
    [Fact]
    public async Task When_an_invalid_request_is_validated_async_should_throw_request_validation_exception()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ISpecification<PlaceOrder>>(OrderSpecification.Create());
        var handler = new SpecificationRequestHandlerAsync<PlaceOrder>(services.BuildServiceProvider());
        var invalidRequest = new PlaceOrder { Sku = "SKU-1", Quantity = 0 };

        //Act
        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => handler.HandleAsync(invalidRequest));

        //Assert
        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(PlaceOrder.Quantity));
    }
}
