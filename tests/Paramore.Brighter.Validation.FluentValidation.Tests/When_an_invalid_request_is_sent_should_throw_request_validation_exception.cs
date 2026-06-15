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

using Paramore.Brighter.RequestValidation;
using Paramore.Brighter.Validation.FluentValidation.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Validation.FluentValidation.Tests
{
    public class InvalidRequestValidationTests
    {
        [Fact]
        public void When_an_invalid_request_is_sent_should_throw_request_validation_exception()
        {
            //Arrange
            var pipeline = ValidationPipeline.With(new GreetingCommandValidator());
            var command = new GreetingCommand { Name = "", Email = "ada@example.com" };

            //Act
            var exception = Assert.Throws<RequestValidationException>(() => pipeline.CommandProcessor.Send(command));

            //Assert
            Assert.False(pipeline.Receipt.Handled);
            var error = Assert.Single(exception.Errors);
            Assert.Equal(nameof(GreetingCommand.Name), error.PropertyName);
            Assert.False(string.IsNullOrWhiteSpace(error.ErrorMessage));
        }
    }
}
