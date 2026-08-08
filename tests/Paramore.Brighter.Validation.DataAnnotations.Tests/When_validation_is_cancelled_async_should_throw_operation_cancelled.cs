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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Validation.DataAnnotations.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Validation.DataAnnotations.Tests;

public class CancelledValidationTests
{
    [Fact]
    public async Task When_validation_is_cancelled_async_should_throw_operation_cancelled()
    {
        //Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var handler = new DataAnnotationsRequestHandlerAsync<RegisterUser>(serviceProvider);
        var request = new RegisterUser { Name = "Ada", Email = "ada@example.com" };
        var cancelled = new CancellationToken(canceled: true);

        //Act //Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(request, cancelled));
    }
}
