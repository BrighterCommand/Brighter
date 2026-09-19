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
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-47 (FR-27.3, FR-8, D6), ADR 0075 step 4 - a Publish subscriber suppresses the ambient scope for
// whatever it nests inside its own handling, even when the subscriber's own pipeline is Transient and
// therefore never took a pipeline scope of its own; a plain Send handler under the same Transient
// lifetime, which is not a subscriber and suppresses nothing, still lets a nested Post adopt the ambient.
public class TransientHandlerNestedPostSuppressionTests
{
    [Fact]
    public async Task When_a_subscriber_takes_no_pipeline_scope_it_should_still_suppress_the_ambient()
    {
        // Arrange - a host whose handler pipelines are all Transient (so neither the Send handler nor
        // the Publish subscriber ever takes a pipeline scope of its own), while the mapper and
        // transformer lifetimes stay Scoped, so a nested Post's own mapper can still ask for the ambient
        await using var factory = new TransientHandlerWebApplicationFactory();
        var client = factory.CreateClient();

        // Act - one request captures its own request-scoped IMarker (R), Sends a command whose Transient
        // handler nests a Post, then Publishes an event whose one, also Transient, subscriber nests a
        // Post of its own
        var response = await client.PostAsync("/api/transient-handler", content: null);
        response.EnsureSuccessStatusCode();

        var recorder = factory.Services.GetRequiredService<TransientHandlerRecorder>();

        // Assert - the Send handler's nested Post resolved R: its own pipeline took no scope, and a
        // plain Send is not a subscriber, so nothing suppresses the ambient it asks for
        Assert.Same(recorder.RequestScopeInstance, recorder.SendNestedInstance);

        // Assert - the Publish subscriber's nested Post did not resolve R: even though the subscriber's
        // own pipeline also took no scope, it is still a subscriber, and suppression still applies
        Assert.NotSame(recorder.RequestScopeInstance, recorder.PublishNestedInstance);
    }
}
