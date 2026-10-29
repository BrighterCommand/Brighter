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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Maps the three documented health endpoints, using only <c>Predicate</c> and <c>ResultStatusCodes</c>, and hosts them on a test server.
/// </summary>
public static class BrighterHealthEndpoints
{
    /// <summary>The liveness endpoint, which selects the restart tag.</summary>
    public const string LivePath = "/health/live";

    /// <summary>The readiness endpoint, which selects the take-out-of-service tag.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>The operator endpoint, which selects the investigate tag and writes the report.</summary>
    public const string OperatorPath = "/health/operator";

    /// <summary>
    /// Maps <see cref="LivePath"/>, <see cref="ReadyPath"/> and <see cref="OperatorPath"/>. Degraded and Unhealthy return 503 on
    /// liveness and readiness.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapBrighterHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(RemedyCheckConvention.RestartTag),
            ResultStatusCodes = FailingStatusCodes()
        });

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(RemedyCheckConvention.TakeOutOfServiceTag),
            ResultStatusCodes = FailingStatusCodes()
        });

        endpoints.MapHealthChecks(OperatorPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(RemedyCheckConvention.InvestigateTag),
            ResponseWriter = BrighterHealthReportJsonWriter.WriteAsync
        });

        return endpoints;
    }

    /// <summary>
    /// Builds and starts a test-server host that has health checks, the registrations from <paramref name="configure"/> and the
    /// three mapped endpoints. The caller disposes the returned application.
    /// </summary>
    /// <param name="configure">Registers the Brighter health checks and their dependencies.</param>
    /// <returns>The started application.</returns>
    public static async Task<WebApplication> StartAsync(Action<IServiceCollection> configure)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();
        configure(builder.Services);

        var app = builder.Build();
        app.MapBrighterHealthEndpoints();
        await app.StartAsync();
        return app;
    }

    private static Dictionary<HealthStatus, int> FailingStatusCodes() => new()
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    };
}
