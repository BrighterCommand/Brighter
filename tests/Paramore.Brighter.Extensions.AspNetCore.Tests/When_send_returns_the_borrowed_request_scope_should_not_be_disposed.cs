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

// An opted-in ASP.NET host with a request-scoped IOrderDbContext. A controller action captures its own
// IOrderDbContext, Sends a command to a handler that shares the same request scope, then uses its
// IOrderDbContext again once Send has returned. The request only completes successfully if that later
// use still works, proving Brighter's borrowed scope did not dispose it. Once the whole HTTP request has
// finished, ASP.NET - not Brighter - must be the one to dispose it, and exactly once.
public class BorrowedRequestScopeNotDisposedTests
{
    [Fact]
    public async Task When_send_returns_the_borrowed_request_scope_should_not_be_disposed()
    {
        // Arrange
        await using var factory = new PlaceOrderWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/orders", content: null);

        // Assert
        // the controller's own post-Send use of its IOrderDbContext only succeeds if Brighter left it usable
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<OrderDbContextRecorder>();
        Assert.NotNull(recorder.ControllerInstance);
        // ASP.NET disposes the request scope once, after the whole HTTP request - including the controller action - has completed
        Assert.Equal(1, recorder.ControllerInstance!.DisposeCount);
    }
}
