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
using Paramore.Brighter.Validation.FluentValidation.Tests.TestDoubles;
using Xunit;
using global::FluentValidation;

namespace Paramore.Brighter.Validation.FluentValidation.Tests
{
    public class ConcurrentValidationTests
    {
        [Fact]
        public void When_the_handler_is_used_concurrently_should_validate_each_request()
        {
            //Arrange
            var services = new ServiceCollection();
            services.AddSingleton<IValidator<GreetingCommand>>(new GreetingCommandValidator());
            var handler = new FluentValidationRequestHandler<GreetingCommand>(services.BuildServiceProvider());

            //Act //Assert: one shared handler instance, hit hard from many threads with a mix of
            //valid and invalid requests; each call must reach the correct outcome with no cross-talk.
            Parallel.For(0, 200, iteration =>
            {
                if (iteration % 2 == 0)
                {
                    var valid = new GreetingCommand { Name = "Ada", Email = "ada@example.com" };
                    var result = handler.Handle(valid);
                    Assert.Same(valid, result);
                }
                else
                {
                    var invalid = new GreetingCommand { Name = "", Email = "" };
                    Assert.Throws<RequestValidationException>(() => handler.Handle(invalid));
                }
            });
        }
    }
}
