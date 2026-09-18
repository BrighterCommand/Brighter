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
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An ASP.NET test host for AC-49 (FR-17) - lifetime triple <c>{Scoped, Scoped, Scoped}</c>,
/// <see cref="IOrderDbContext"/> registered <c>AddScoped</c>, and <see cref="PlaceOrderController"/> as its
/// one controller, that calls <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/> <b>twice</b>
/// - once with <paramref name="firstCall"/>'s affinity, once with <paramref name="secondCall"/>'s - around
/// the single <c>AddBrighter</c> call, then <c>ValidatePipelines(throwOnError: true)</c> last, so a warning
/// alone must never fail startup.
/// </summary>
public sealed class RepeatedRequestScopeWebApplicationFactory : WebApplicationFactory<PlaceOrderController>
{
    private readonly ScopeAffinity _firstCall;
    private readonly ScopeAffinity _secondCall;
    private readonly ILoggerProvider _loggerProvider;

    /// <param name="firstCall">The affinity passed to the first <c>AddBrighterRequestScope</c> call.</param>
    /// <param name="secondCall">The affinity passed to the second, later <c>AddBrighterRequestScope</c>
    /// call - the one D18 makes effective.</param>
    /// <param name="loggerProvider">Registered on the host so a test can assert on the validator's logged
    /// findings.</param>
    public RepeatedRequestScopeWebApplicationFactory(
        ScopeAffinity firstCall, ScopeAffinity secondCall, ILoggerProvider loggerProvider)
    {
        _firstCall = firstCall;
        _secondCall = secondCall;
        _loggerProvider = loggerProvider;
    }

    /// <summary>
    /// Pins the content root to the test assembly's own output directory, mirroring
    /// <see cref="DefeatedOptInWebApplicationFactory"/> - the test assembly is the entry point, so the base
    /// class's own content-root discovery resolves to a path that does not exist.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    /// <inheritdoc />
    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                ConfigureWebHost(webBuilder);
            });
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // WebApplicationFactory invokes ConfigureServices twice against the same collection (T6.10).
            // ValidatePipelines()'s validator/hosted-service registrations and AddProvider's logger-provider
            // registration are plain AddSingleton, so a second invocation would duplicate them and multiply
            // every logged finding - guard the whole block on a sentinel this fixture alone registers, as
            // DefeatedOptInWebApplicationFactory already does.
            if (services.Any(d => d.ServiceType == typeof(OrderDbContextRecorder))) return;

            services.AddControllers().AddApplicationPart(typeof(PlaceOrderController).Assembly);
            services.AddScoped<IOrderDbContext, OrderDbContext>();
            services.AddSingleton<OrderDbContextRecorder>();
            services.AddLogging(logging => logging.AddProvider(_loggerProvider));

            services.AddBrighterRequestScope(_firstCall);
            var brighterBuilder = services.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
            });
            services.AddBrighterRequestScope(_secondCall);
            brighterBuilder.ValidatePipelines(throwOnError: true);
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
