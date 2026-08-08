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

using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.Validation.Specification.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Validation.Specification.Tests;

public class UseSpecificationRegistrationTests
{
    [Fact]
    public void When_use_specification_validation_is_registered_should_validate_through_the_pipeline()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddSingleton<HandlerReceipt>();
        services.AddSingleton<ISpecification<PlaceOrder>>(OrderSpecification.Create());
        services.AddBrighter().UseSpecification();
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        var invalidRequest = new PlaceOrder { Sku = "", Quantity = 0 };

        //Act
        var exception = Assert.Throws<RequestValidationException>(() => commandProcessor.Send(invalidRequest));

        //Assert
        Assert.NotEmpty(exception.Errors);
    }
}
