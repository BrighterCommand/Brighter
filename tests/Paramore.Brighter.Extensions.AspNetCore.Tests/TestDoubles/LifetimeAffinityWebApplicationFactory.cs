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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An ASP.NET test host opted in to <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>
/// (AC-26), parameterised by the one lifetime every one of <c>HandlerLifetime</c>/<c>MapperLifetime</c>/
/// <c>TransformerLifetime</c> is set to, and by the affinity passed to the extension itself (never
/// assigned as the host's own <c>DefaultScopeAffinity</c>). Hosts both <see cref="PlaceOrderController"/>
/// (whose <see cref="IOrderDbContext"/> is registered at the same lifetime, for the
/// <c>Transient</c>/<c>Scoped</c> runs) and <see cref="PlaceSingletonOrderController"/> (whose
/// <see cref="ISingletonDependency"/> is always registered <c>AddSingleton</c>, for the
/// <c>Singleton</c> run).
/// </summary>
public sealed class LifetimeAffinityWebApplicationFactory : WebApplicationFactory<PlaceOrderController>
{
    private readonly ServiceLifetime _lifetime;
    private readonly ScopeAffinity _affinity;

    /// <param name="lifetime">The one lifetime assigned to <c>HandlerLifetime</c>, <c>MapperLifetime</c>
    /// and <c>TransformerLifetime</c> alike, and to <see cref="IOrderDbContext"/>'s own registration.</param>
    /// <param name="affinity">The affinity passed to
    /// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>.</param>
    public LifetimeAffinityWebApplicationFactory(ServiceLifetime lifetime, ScopeAffinity affinity)
    {
        _lifetime = lifetime;
        _affinity = affinity;
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
            services.AddControllers().AddApplicationPart(typeof(PlaceOrderController).Assembly);
            services.Add(new ServiceDescriptor(typeof(IOrderDbContext), typeof(OrderDbContext), _lifetime));
            services.AddSingleton<OrderDbContextRecorder>();
            services.AddSingleton<ISingletonDependency, SingletonDependency>();
            services.AddSingleton<SingletonOrderRecorder>();

            services.AddBrighterRequestScope(_affinity);
            services.AddBrighter(options =>
            {
                options.HandlerLifetime = _lifetime;
                options.MapperLifetime = _lifetime;
                options.TransformerLifetime = _lifetime;
            });
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
