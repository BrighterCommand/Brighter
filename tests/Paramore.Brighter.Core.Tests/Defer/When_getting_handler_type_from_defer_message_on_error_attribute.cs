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

using Paramore.Brighter.Defer.Attributes;
using Paramore.Brighter.Defer.Handlers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Defer
{
    public class When_getting_handler_type_from_defer_message_on_error_attribute
    {
        [Fact]
        public void It_should_return_correct_sync_handler_configuration()
        {
            //Arrange
            var attribute = new DeferMessageOnErrorAttribute(step: 1, delayMilliseconds: 5000);

            //Act
            var handlerType = attribute.GetHandlerType();
            var initializerParams = attribute.InitializerParams();

            //Assert
            Assert.Equal(typeof(DeferMessageOnErrorHandler<>), handlerType); // Returns the correct handler type
            Assert.Equal(HandlerTiming.Before, attribute.Timing); // Must wrap subsequent handlers
            Assert.Equal(1, attribute.Step); // Preserves the specified step
            Assert.Single(initializerParams); // Has one initializer parameter
            Assert.Equal(5000, initializerParams[0]); // Delay value flows through
        }

        [Fact]
        public void It_should_return_correct_async_handler_configuration()
        {
            //Arrange
            var attribute = new DeferMessageOnErrorAsyncAttribute(step: 2, delayMilliseconds: 3000);

            //Act
            var handlerType = attribute.GetHandlerType();
            var initializerParams = attribute.InitializerParams();

            //Assert
            Assert.Equal(typeof(DeferMessageOnErrorHandlerAsync<>), handlerType); // Returns the correct async handler type
            Assert.Equal(HandlerTiming.Before, attribute.Timing); // Must wrap subsequent handlers
            Assert.Equal(2, attribute.Step); // Preserves the specified step
            Assert.Single(initializerParams); // Has one initializer parameter
            Assert.Equal(3000, initializerParams[0]); // Delay value flows through
        }
    }
}
