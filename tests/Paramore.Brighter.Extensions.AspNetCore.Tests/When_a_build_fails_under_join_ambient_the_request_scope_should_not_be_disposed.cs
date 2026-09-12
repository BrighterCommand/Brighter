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

// AC-38 (FR-5, FR-12) - a failed build under JoinAmbient disposes nothing the caller owns. An opted-in
// ASP.NET host with a mapper whose constructor depends on a service never registered in the container,
// so every attempt to build the transform pipeline for that command throws. A controller action Posts
// that command 100 times, each expected to fail with a ConfigurationException wrapping the container's
// own InvalidOperationException, then uses its own Scoped IOrderDbContext once more before returning.
// The request only completes successfully, and that later use only succeeds, if none of the 100 failed
// builds disposed anything the request scope owns - which holds because, under adoption, no Brighter-
// owned scope was ever created to release in the first place.
public class FailedBuildScopeDisposalTests
{
    [Fact]
    public async Task When_a_build_fails_under_join_ambient_the_request_scope_should_not_be_disposed()
    {
        // Arrange
        await using var factory = new FailedBuildWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/failed-builds", content: null);

        // Assert - every attempt failed exactly as expected, and none produced a surprising outcome
        response.EnsureSuccessStatusCode();
        var recorder = factory.Services.GetRequiredService<FailedBuildRecorder>();
        Assert.Equal(100, recorder.ExpectedFailureCount);
        Assert.Equal(0, recorder.UnexpectedOutcomeCount);

        // Assert - the request scope's own IOrderDbContext was still usable and undisposed immediately
        // after the 100 failures, proving none of them disposed anything the request scope owns
        Assert.True(recorder.RequestScopeUsableAfterFailures);
        Assert.Equal(0, recorder.OrderDbContextDisposeCountAfterFailures);

        // Assert - every one of the 100 failed builds genuinely asked to adopt the ambient, so the
        // "because under adoption none was created" premise of the criterion actually held here
        var scopeProvider = (DelegatingScopeProviderRecorder)factory.Services.GetRequiredService<IAmAScopeProvider>();
        Assert.Equal(100, scopeProvider.Decisions.Count);
        Assert.All(scopeProvider.Decisions, decision => Assert.Equal(ScopeAffinity.JoinAmbient, decision));
    }
}
