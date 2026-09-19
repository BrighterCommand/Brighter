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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-15 (FR-17, FR-14) - an ASP.NET host opted in via AddBrighterRequestScope(), lifetime triple
// {Scoped, Scoped, Scoped}, IOrderDbContext registered AddScoped. A controller action captures its own
// IOrderDbContext and Sends a command whose handler also takes IOrderDbContext: the handler's instance
// must be reference-equal to the controller's, proving the Send pipeline adopted the request's own DI
// scope rather than creating its own.
public class ControllerSendSharesRequestScopeTests
{
    [Fact]
    public async Task When_a_controller_sends_in_an_opted_in_host_the_handler_should_share_the_request_scope()
    {
        // Arrange
        await using var factory = new PlaceOrderWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotNull(recorder.ControllerInstance);
        Assert.NotNull(recorder.HandlerInstance);
        Assert.Same(recorder.ControllerInstance, recorder.HandlerInstance);
    }
}
