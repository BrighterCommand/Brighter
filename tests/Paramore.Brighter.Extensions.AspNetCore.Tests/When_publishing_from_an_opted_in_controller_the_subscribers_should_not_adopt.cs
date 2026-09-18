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

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-11 (FR-8, FR-9(a), FR-10, FR-24.4, D6, D19), ADR 0075 steps 3, 6, ADR 0072 step 2 - a Publish
// subscriber's own pipeline is isolated from whatever ambient scope its caller carries, even in an
// opted-in host, and FR-10 holds even when the registered provider itself does not honour it: Brighter
// ignores an ambient handed over for an AlwaysNew ask and warns about it exactly once per container.
public class PublishSubscriberAdoptionTests
{
    [Fact]
    public async Task When_publishing_from_an_opted_in_controller_the_subscribers_should_not_adopt()
    {
        // Arrange - the opted-in host from T6.3 (AC-15), plus this test's own recorder
        await using var factory = new PlaceOrderWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddSingleton<PublishScopeRecorder>());
        });
        var client = factory.CreateClient();

        // Act - a controller action captures its own request-scoped IOrderDbContext (R), then
        // PublishAsyncs to two subscribers each taking IOrderDbContext
        var response = await client.PostAsync("/api/publish-order", content: null);
        response.EnsureSuccessStatusCode();

        // Assert - neither subscriber resolved R, and the two resolved two distinct instances
        var recorder = factory.Services.GetRequiredService<PublishScopeRecorder>();
        var requestScopeInstance = recorder.RequestScopeInstance;
        var subscriberInstances = recorder.SubscriberInstances.ToArray();
        Assert.Equal(2, subscriberInstances.Length);
        Assert.DoesNotContain(subscriberInstances, instance => ReferenceEquals(instance, requestScopeInstance));
        Assert.NotSame(subscriberInstances[0], subscriberInstances[1]);

        // Assert - both subscriber instances were disposed when PublishAsync completed, and R was not
        Assert.All(recorder.SubscriberDisposeCountsAfterPublish, disposeCount => Assert.Equal(1, disposeCount));
        Assert.Equal(0, recorder.RequestScopeDisposeCountAfterPublish);
    }

    [Fact]
    public async Task When_an_affinity_ignoring_provider_offers_the_ambient_for_an_always_new_ask_it_should_be_ignored_and_warned_once()
    {
        // Arrange - the same shape of application, except its only IAmAScopeProvider is a hand-rolled one
        // that returns the request's own ambient for every ask, including AlwaysNew ones (violating FR-10)
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new AffinityIgnoringProviderWebApplicationFactory(
            offerForAlwaysNew: true, offerForJoinAmbient: true).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(capturingProvider)));
        });
        var client = factory.CreateClient();

        // Act - PublishAsync twice; each carries two AlwaysNew asks (one per subscriber)
        var first = await client.PostAsync("/api/publish-order", content: null);
        var second = await client.PostAsync("/api/publish-order", content: null);

        // Assert - the outcome is unchanged: both requests still succeeded
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        // Assert - exactly one warning across both calls, naming the ignored-for-AlwaysNew condition and
        // the provider's own implementation type - the latch is once per (condition, provider type) pair
        // for the whole container
        var warnings = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        var warning = Assert.Single(warnings);
        Assert.Contains("AmbientIgnoredForAlwaysNew", warning.Message);
        Assert.Contains(nameof(AffinityIgnoringScopeProvider), warning.Message);
    }

    [Fact]
    public async Task When_two_hosts_share_the_same_affinity_ignoring_provider_type_each_should_latch_independently()
    {
        // Arrange - a second host of the same shape, registering the same provider implementation type,
        // but inverted: it offers the ambient for AlwaysNew asks and withholds it for JoinAmbient ones
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new AffinityIgnoringProviderWebApplicationFactory(
            offerForAlwaysNew: true, offerForJoinAmbient: false).WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => services.AddLogging(logging => logging.AddProvider(capturingProvider)));
        });
        var client = factory.CreateClient();

        // Act - a Send (one JoinAmbient ask, from the Scoped handler pipeline) then a PublishAsync to two
        // subscribers (two AlwaysNew asks, forced by Publish's own isolation)
        (await client.PostAsync("/api/orders", content: null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/publish-order", content: null)).EnsureSuccessStatusCode();

        // Assert - exactly two warnings: one no-ambient-offered (the Send), one ignored-for-AlwaysNew (the
        // first subscriber to ask; the second subscriber's identical ask is already latched) - not one
        // (which a shared or provider-type-only latch would produce) and not more
        var warningsAfterFirstRound = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(2, warningsAfterFirstRound.Count);
        Assert.Contains(warningsAfterFirstRound, w => w.Message.Contains("NoAmbientOffered"));
        Assert.Contains(warningsAfterFirstRound, w => w.Message.Contains("AmbientIgnoredForAlwaysNew"));
        Assert.All(warningsAfterFirstRound, w => Assert.Contains(nameof(AffinityIgnoringScopeProvider), w.Message));

        // Act - repeat both operations
        (await client.PostAsync("/api/orders", content: null)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/publish-order", content: null)).EnsureSuccessStatusCode();

        // Assert - no further entry
        var warningsAfterSecondRound = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(2, warningsAfterSecondRound.Count);
    }
}
