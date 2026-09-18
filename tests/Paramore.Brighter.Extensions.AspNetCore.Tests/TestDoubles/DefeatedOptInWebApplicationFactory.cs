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
/// An ASP.NET test host for AC-50 (FR-22.4) - lifetime triple <c>{Scoped, Scoped, Scoped}</c>,
/// <see cref="IOrderDbContext"/> registered <c>AddScoped</c>, and <see cref="PlaceOrderController"/> as
/// its one controller, exactly as <see cref="LifetimeAffinityWebApplicationFactory"/>'s all-<c>Scoped</c>
/// shape - except that when <paramref name="applicationRegistersOwnOptions"/> is true, the host also
/// registers its own <see cref="IBrighterOptions"/> instance before calling <c>AddBrighter</c>, which
/// <c>RegisterBrighterOptions</c> (ADR 0076) then leaves alone (first-registration-wins), defeating
/// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>'s write-through. Always calls
/// <c>ValidatePipelines(throwOnError: false)</c> last, so a defeated host still starts and its controller
/// action still runs.
/// </summary>
public sealed class DefeatedOptInWebApplicationFactory : WebApplicationFactory<PlaceOrderController>
{
    private readonly bool _applicationRegistersOwnOptions;
    private readonly ILoggerProvider _loggerProvider;

    /// <param name="applicationRegistersOwnOptions">Whether the host registers its own
    /// <see cref="IBrighterOptions"/> instance before <c>AddBrighter</c> - the defeating registration. When
    /// false, this is the control host: no application registration, so nothing defeats the opt-in.</param>
    /// <param name="loggerProvider">Registered on the host so a test can assert on the validator's logged
    /// findings.</param>
    public DefeatedOptInWebApplicationFactory(bool applicationRegistersOwnOptions, ILoggerProvider loggerProvider)
    {
        _applicationRegistersOwnOptions = applicationRegistersOwnOptions;
        _loggerProvider = loggerProvider;
    }

    /// <summary>
    /// Pins the content root to the test assembly's own output directory, mirroring
    /// <see cref="PlaceOrderWebApplicationFactory"/> - the test assembly is the entry point, so the base
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
            // WebApplicationFactory invokes ConfigureServices twice against the same collection while
            // building its own throwaway discovery host (T6.10). Most of this wiring is TryAdd-shaped and
            // tolerates that harmlessly, but ValidatePipelines()'s validator/hosted-service registrations
            // and AddProvider's logger-provider registration are deliberately plain AddSingleton (so more
            // than one validator, or more than one host, can coexist) - a second invocation would
            // duplicate them and multiply every logged finding. Guard the whole block on a sentinel this
            // fixture alone registers, so the second invocation is a no-op.
            if (services.Any(d => d.ServiceType == typeof(OrderDbContextRecorder))) return;

            services.AddControllers().AddApplicationPart(typeof(PlaceOrderController).Assembly);
            services.AddScoped<IOrderDbContext, OrderDbContext>();
            services.AddSingleton<OrderDbContextRecorder>();
            services.AddLogging(logging => logging.AddProvider(_loggerProvider));

            if (_applicationRegistersOwnOptions)
            {
                services.AddSingleton<IBrighterOptions>(new BrighterOptions
                {
                    HandlerLifetime = ServiceLifetime.Scoped,
                    MapperLifetime = ServiceLifetime.Scoped,
                    TransformerLifetime = ServiceLifetime.Scoped
                });
            }

            var brighterBuilder = services.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
            });
            services.AddBrighterRequestScope();
            brighterBuilder.ValidatePipelines(throwOnError: false);
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
