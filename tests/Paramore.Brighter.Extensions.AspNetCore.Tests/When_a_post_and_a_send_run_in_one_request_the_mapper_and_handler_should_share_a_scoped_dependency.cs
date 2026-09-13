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

// AC-34 (FR-16b, C-3's exception), ADR 0072 step 4 - in an opted-in host, a Post's mapper and a Send's
// handler in the same request both resolve IMarker AddScoped from the request scope: the two must be
// reference-equal, and neither disposed until ASP.NET disposes the request scope at end of request. This
// is the producer-side counterpart to AC-21 (T1.13), whose consumer-side outcome is the opposite -
// deliberately, since there the transform pipeline's scope is Brighter-owned, not borrowed.
public class PostAndSendShareScopedDependencyTests
{
    [Fact]
    public async Task When_a_post_and_a_send_run_in_one_request_the_mapper_and_handler_should_share_a_scoped_dependency()
    {
        // Arrange
        await using var factory = new PlaceOrderWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/shared-dependency", content: null);

        // Assert
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<SharedDependencyRecorder>();
        Assert.NotNull(recorder.MapperMarker);
        Assert.NotNull(recorder.HandlerMarker);
        // the Post's mapper and the Send's handler resolved the same Scoped IMarker from the request scope
        Assert.Same(recorder.MapperMarker, recorder.HandlerMarker);
        // neither was disposed while still inside the controller action
        Assert.Equal(0, recorder.DisposeCountAfterAction);
        // ASP.NET disposes the request scope, and the shared IMarker with it, exactly once, once the whole HTTP request has completed
        Assert.Equal(1, recorder.MapperMarker!.DisposeCount);
    }
}
