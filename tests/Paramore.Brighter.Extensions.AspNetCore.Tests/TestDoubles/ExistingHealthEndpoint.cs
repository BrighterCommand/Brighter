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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Maps an application's existing health endpoint with the default response writer, either excluding the checks tagged with the
/// Brighter remedy tags or, as a hazard, with no predicate at all, and hosts it on a test server.
/// </summary>
public static class ExistingHealthEndpoint
{
    /// <summary>The existing endpoint.</summary>
    public const string Path = "/health";

    /// <summary>
    /// Maps <see cref="Path"/> with the default writer and the documented predicate that excludes every check tagged with a Brighter remedy tag.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapExistingHealthEndpointExcludingRemedyChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            Predicate = registration => !registration.Tags.Contains(RemedyCheckConvention.RestartTag)
                                        && !registration.Tags.Contains(RemedyCheckConvention.TakeOutOfServiceTag)
                                        && !registration.Tags.Contains(RemedyCheckConvention.InvestigateTag)
        });

        return endpoints;
    }

    /// <summary>
    /// Maps <see cref="Path"/> with the default writer and no predicate, so it selects every registered check.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapExistingHealthEndpointWithoutPredicate(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path);
        return endpoints;
    }

    /// <summary>
    /// Builds and starts a test-server host with health checks, the registrations from <paramref name="configure"/> and the endpoint
    /// mapped by <paramref name="map"/>. The caller disposes the returned application.
    /// </summary>
    /// <param name="configure">Registers the health checks and their dependencies.</param>
    /// <param name="map">Maps the existing endpoint.</param>
    /// <returns>The started application.</returns>
    public static async Task<WebApplication> StartAsync(Action<IServiceCollection> configure, Action<IEndpointRouteBuilder> map)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();
        configure(builder.Services);

        var app = builder.Build();
        map(app);
        await app.StartAsync();
        return app;
    }
}
