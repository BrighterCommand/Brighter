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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-37 (FR-26, NFR-5, NFR-6, D7) - the per-request association a borrowed scope holds between a Scoped
// artefact and the DI scope that produced it must not accumulate as a host serves more requests. Four
// facts: clause 1 sends one request, captures a WeakReference to that request's own mapper instance, then
// sends 10,000 further requests and forces a collection - the first mapper must have become unreachable.
// Clause 2 is the positive control that makes clause 1 falsifiable: the identical harness run against a
// host where the association is deliberately made process-lifetime (ScopedArtefactCache re-registered
// Singleton, through the same production resolution path every request actually uses) must report the
// captured instance as still reachable - proving the WeakReference/GC.Collect mechanism itself would have
// caught an accumulation bug had one been present. Clauses 3 and 4 measure the peak number of live
// per-scope associations, sampled while requests are in flight, at concurrency 1 and concurrency 8 over
// 10,000 requests each - both peaks must stay a small, bounded multiple of the concurrency level, not grow
// toward the request count. Because ScopedArtefactCache's construction/disposal counter is a single
// process-wide static and other test classes in this assembly build their own Brighter hosts concurrently
// under xUnit's default parallel-by-class execution, clauses 3 and 4 measure the peak relative to a
// baseline sampled immediately before each run, rather than an absolute count, so unrelated concurrent
// activity elsewhere in the process cannot make either assertion falsely fail.
public class BorrowedScopeAccumulationTests
{
    private const int FurtherRequestCount = 10_000;

    [Fact]
    public async Task When_serving_many_requests_borrowed_scope_state_should_not_accumulate()
    {
        // Arrange - one request, captured as a WeakReference so nothing this test owns keeps it alive
        await using var factory = new AccumulationWebApplicationFactory();
        using var client = factory.CreateClient();
        var weakReference = await CaptureFirstRequestMapperAsync(factory, client);

        // Act - 10,000 further requests, each with its own request scope and its own mapper instance
        await PostManyTimesAsync(client, FurtherRequestCount);
        ForceFullCollection();

        // Assert - the first request's mapper is no longer reachable from anything Brighter or ASP.NET held
        Assert.False(weakReference.IsAlive);
    }

    [Fact]
    public async Task When_the_association_is_made_process_lifetime_it_should_remain_reachable()
    {
        // Arrange - identical harness, but ScopedArtefactCache is Singleton, not Scoped, on this host
        await using var factory = new AccumulationRetainingWebApplicationFactory();
        using var client = factory.CreateClient();
        var weakReference = await CaptureFirstRequestMapperAsync(factory, client);

        // Act
        await PostManyTimesAsync(client, FurtherRequestCount);
        ForceFullCollection();

        // Assert - the association was deliberately made process-lifetime, so the first mapper is still reachable
        Assert.True(weakReference.IsAlive);
    }

    [Fact]
    public async Task When_requests_are_served_at_concurrency_one_the_peak_live_count_should_stay_bounded()
    {
        // Arrange
        await using var factory = new AccumulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const int concurrency = 1;

        // Act
        var peakAboveBaseline = await MeasurePeakLiveScopedArtefactCachesAsync(client, concurrency, FurtherRequestCount);

        // Assert - a small, bounded multiple of the concurrency level, nowhere near the 10,000 requests served
        Assert.True(peakAboveBaseline <= concurrency * 8,
            $"expected the peak live count above baseline to stay within {concurrency * 8}, but it reached {peakAboveBaseline}");
    }

    [Fact]
    public async Task When_requests_are_served_at_concurrency_eight_the_peak_live_count_should_stay_bounded()
    {
        // Arrange
        await using var factory = new AccumulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const int concurrency = 8;

        // Act
        var peakAboveBaseline = await MeasurePeakLiveScopedArtefactCachesAsync(client, concurrency, FurtherRequestCount);

        // Assert - a small, bounded multiple of the concurrency level, nowhere near the 10,000 requests served
        Assert.True(peakAboveBaseline <= concurrency * 8,
            $"expected the peak live count above baseline to stay within {concurrency * 8}, but it reached {peakAboveBaseline}");
    }

    /// <summary>
    /// Sends one request, then returns a non-resurrecting <see cref="WeakReference"/> to the mapper
    /// instance its pipeline resolved, with no strong reference of this method's own left behind.
    /// </summary>
    private static async Task<WeakReference> CaptureFirstRequestMapperAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<AccumulationController> factory, HttpClient client)
    {
        var response = await client.PostAsync("/api/accumulate", content: null);
        response.EnsureSuccessStatusCode();

        var recorder = factory.Services.GetRequiredService<AccumulationRecorder>();
        var mapper = recorder.LastConstructedMapper;
        Assert.NotNull(mapper);

        return new WeakReference(mapper, trackResurrection: false);
    }

    private static async Task PostManyTimesAsync(HttpClient client, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var response = await client.PostAsync("/api/accumulate", content: null);
            response.EnsureSuccessStatusCode();
        }
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// Serves <paramref name="requestCount"/> requests, never more than <paramref name="concurrency"/> in
    /// flight at once, sampling <see cref="ScopedArtefactCache.LiveCount"/> throughout, and returns the
    /// highest value observed above the count already live when this method started.
    /// </summary>
    private static async Task<int> MeasurePeakLiveScopedArtefactCachesAsync(HttpClient client, int concurrency, int requestCount)
    {
        var baseline = ScopedArtefactCache.LiveCount;
        var peak = baseline;

        using var samplingCts = new CancellationTokenSource();
        var samplingTask = Task.Run(async () =>
        {
            while (!samplingCts.IsCancellationRequested)
            {
                var observed = ScopedArtefactCache.LiveCount;
                int current;
                do
                {
                    current = peak;
                    if (observed <= current) break;
                } while (Interlocked.CompareExchange(ref peak, observed, current) != current);

                await Task.Delay(1);
            }
        });

        using var gate = new SemaphoreSlim(concurrency);
        var requests = new List<Task>(requestCount);
        for (var i = 0; i < requestCount; i++)
        {
            await gate.WaitAsync();
            requests.Add(Task.Run(async () =>
            {
                try
                {
                    var response = await client.PostAsync("/api/accumulate", content: null);
                    response.EnsureSuccessStatusCode();
                }
                finally
                {
                    gate.Release();
                }
            }));
        }

        await Task.WhenAll(requests);

        samplingCts.Cancel();
        await samplingTask;

        return peak - baseline;
    }
}
