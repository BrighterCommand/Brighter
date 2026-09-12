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

// An opted-in ASP.NET host with a Scoped mapper. One controller action Posts two commands, both mapped
// by the same mapper type. The two Posts must resolve the same mapper instance from the request scope,
// neither Post may dispose it, and it is only ASP.NET that disposes it, once, when the whole HTTP request
// has completed.
public class TwoPostsShareOneScopedMapperTests
{
    [Fact]
    public async Task When_two_posts_run_in_one_request_they_should_share_one_scoped_mapper()
    {
        // Arrange
        await using var factory = new PlaceOrderWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/posted-orders", content: null);

        // Assert
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<PostedOrderMapperRecorder>();
        // both Posts resolved the same mapper instance - only one was ever constructed
        Assert.Single(recorder.Constructed);
        // the shared mapper was not disposed when the second Post returned, still inside the controller action
        Assert.Equal(0, recorder.DisposeCountAfterSecondPost);
        // ASP.NET disposes the request scope, and the mapper with it, exactly once, once the whole HTTP request has completed
        Assert.Equal(1, recorder.Constructed[0].DisposeCount);
    }
}
