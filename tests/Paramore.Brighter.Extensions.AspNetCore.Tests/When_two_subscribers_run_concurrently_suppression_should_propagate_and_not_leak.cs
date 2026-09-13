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

// AC-12 (FR-8, FR-9, NFR-4, D6, D10), ADR 0075 steps 3, 4 - suppression must hold for a Publish
// subscriber's whole execution, not merely its own resolution, so that a nested pipeline it issues
// mid-handler is suppressed too; and once the publish returns, the caller's own flow must not be left
// suppressed, so a Send or Post issued next adopts the request scope exactly as it would have before
// the publish.
public class ConcurrentPublishSuppressionTests
{
    [Fact]
    public async Task When_two_subscribers_run_concurrently_suppression_should_propagate_and_not_leak()
    {
        // Arrange - the opted-in host from T6.3, plus this test's own recorder
        await using var factory = new PlaceOrderWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<ConcurrentPublishRecorder>());
        });
        var client = factory.CreateClient();

        // Act - a controller action captures its own request-scoped IOrderDbContext (R), then
        // PublishAsyncs to two subscribers that each arrive at a shared rendezvous so their
        // executions provably overlap; the first subscriber also issues a nested SendAsync from
        // inside its own HandleAsync, after the rendezvous. Once the publish returns, the controller
        // issues a Send and a Post of its own
        var response = await client.PostAsync("/api/concurrent-publish", content: null);
        response.EnsureSuccessStatusCode();

        var recorder = factory.Services.GetRequiredService<ConcurrentPublishRecorder>();

        // Assert - both subscribers were in flight simultaneously: neither would have observed the
        // other's arrival at the rendezvous if Publish had run them one at a time
        Assert.True(recorder.SubscriberOneObservedOverlap);
        Assert.True(recorder.SubscriberTwoObservedOverlap);

        // Assert - neither subscriber's own IOrderDbContext is R
        Assert.NotSame(recorder.RequestScopeInstance, recorder.SubscriberOneInstance);
        Assert.NotSame(recorder.RequestScopeInstance, recorder.SubscriberTwoInstance);

        // Assert - the nested SendAsync issued from inside the first subscriber's own HandleAsync
        // resolved an IOrderDbContext that is not R, not the first subscriber's own, and not the
        // second subscriber's own
        Assert.NotSame(recorder.RequestScopeInstance, recorder.InnerCommandInstance);
        Assert.NotSame(recorder.SubscriberOneInstance, recorder.InnerCommandInstance);
        Assert.NotSame(recorder.SubscriberTwoInstance, recorder.InnerCommandInstance);

        // Assert - once the publish completed, a Send and a Post issued from the controller outside
        // any subscriber both resolved from R's scope - the assertions that would fail on a leak
        Assert.Same(recorder.RequestScopeInstance, recorder.OutsideSendInstance);
        Assert.Same(recorder.RequestScopeInstance, recorder.OutsidePostInstance);
    }
}
