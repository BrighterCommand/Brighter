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

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;


namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-39 (FR-8, FR-9, NFR-4, D6, OOS-14), ADR 0075 steps 3, 4, 5, 5a - the synchronous Publish path
// isolates every subscriber from the caller's own request scope, and from each other, exactly as the
// asynchronous path does; a pipeline a subscriber nests inside its own Handle is isolated too, from both
// the request scope and every other subscriber's own pipeline; and once Publish returns, the caller's
// own flow is not left suppressed, so a Send and a Post issued next both adopt the request scope again.
public class SynchronousPublishSuppressionTests
{
    [Test]
    public async Task When_publishing_synchronously_each_subscriber_should_be_isolated_and_the_caller_left_unsuppressed()
    {
        // Arrange - the opted-in host from T6.3, plus this test's own recorder
        await using var factory = new PlaceOrderWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<SyncPublishRecorder>());
        });
        var client = factory.CreateClient();

        // Act - a controller action captures its own request-scoped IOrderDbContext (R), then calls the
        // synchronous Publish to three subscribers, two of which each issue a nested Send from inside
        // their own Handle. Once Publish returns, the controller issues a Send and a Post of its own
        var response = await client.PostAsync("/api/sync-publish", content: null);
        response.EnsureSuccessStatusCode();

        var recorder = factory.Services.GetRequiredService<SyncPublishRecorder>();
        var requestScopeInstance = recorder.RequestScopeInstance;

        // Assert - all three subscribers resolved distinct instances, and none of them is R
        var subscriberInstances = recorder.SubscriberInstances;
        await Assert.That(subscriberInstances.Count).IsEqualTo(3);
        var one = subscriberInstances[SyncPublishSubscriberOne.Marker];
        var two = subscriberInstances[SyncPublishSubscriberTwo.Marker];
        var three = subscriberInstances[SyncPublishSubscriberThree.Marker];
        await Assert.That(one).IsNotSameReferenceAs(requestScopeInstance);
        await Assert.That(two).IsNotSameReferenceAs(requestScopeInstance);
        await Assert.That(three).IsNotSameReferenceAs(requestScopeInstance);
        await Assert.That(two).IsNotSameReferenceAs(one);
        await Assert.That(three).IsNotSameReferenceAs(one);
        await Assert.That(three).IsNotSameReferenceAs(two);

        // Assert - each nesting subscriber's own nested Send resolved an instance that is neither R, nor
        // its own subscriber's, nor the other nesting subscriber's own instance or its nested Send's -
        // identified by each subscriber's own marker, not by which one happened to run first
        var nestedSendInstances = recorder.NestedSendInstances;
        await Assert.That(nestedSendInstances.Count).IsEqualTo(2);
        var innerOne = nestedSendInstances[SyncPublishSubscriberOne.Marker];
        var innerTwo = nestedSendInstances[SyncPublishSubscriberTwo.Marker];
        await Assert.That(innerOne).IsNotSameReferenceAs(requestScopeInstance);
        await Assert.That(innerOne).IsNotSameReferenceAs(one);
        await Assert.That(innerOne).IsNotSameReferenceAs(two);
        await Assert.That(innerOne).IsNotSameReferenceAs(innerTwo);
        await Assert.That(innerTwo).IsNotSameReferenceAs(requestScopeInstance);
        await Assert.That(innerTwo).IsNotSameReferenceAs(two);
        await Assert.That(innerTwo).IsNotSameReferenceAs(one);
        await Assert.That(innerTwo).IsNotSameReferenceAs(innerOne);

        // Assert - once the publish completed, a Send and a Post issued from the controller outside any
        // subscriber both resolved from R's scope - the assertions that would fail on a leak
        await Assert.That(recorder.OutsideSendInstance).IsSameReferenceAs(requestScopeInstance);
        await Assert.That(recorder.OutsidePostInstance).IsSameReferenceAs(requestScopeInstance);
    }
}
