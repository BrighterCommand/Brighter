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
/// An ASP.NET test host whose own <c>DefaultScopeAffinity</c>, the affinity argument passed to
/// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>, and the registration order
/// between that extension call and <c>AddBrighter</c> are all supplied by the test (AC-18). Lifetime
/// triple <c>{Scoped, Scoped, Scoped}</c>, one controller (<see cref="PlaceOrderController"/>), and a
/// <see cref="DelegatingScopeProviderRecorder"/> registered last so it is the effective ambient source
/// regardless of that order.
/// </summary>
public sealed class RegistrationOrderWebApplicationFactory : WebApplicationFactory<PlaceOrderController>
{
    private readonly ScopeAffinity _hostDefaultAffinity;
    private readonly ScopeAffinity? _extensionAffinityArgument;
    private readonly bool _extensionCallBeforeAddBrighter;

    /// <param name="hostDefaultAffinity">The value the host's own <c>AddBrighter</c> delegate assigns to
    /// <c>DefaultScopeAffinity</c>.</param>
    /// <param name="extensionAffinityArgument">The affinity passed to
    /// <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>, or <see langword="null"/> to
    /// call it with no argument, taking its <see cref="ScopeAffinity.JoinAmbient"/> default.</param>
    /// <param name="extensionCallBeforeAddBrighter">Whether the extension call is registered before
    /// <c>AddBrighter</c> rather than after.</param>
    public RegistrationOrderWebApplicationFactory(
        ScopeAffinity hostDefaultAffinity,
        ScopeAffinity? extensionAffinityArgument,
        bool extensionCallBeforeAddBrighter)
    {
        _hostDefaultAffinity = hostDefaultAffinity;
        _extensionAffinityArgument = extensionAffinityArgument;
        _extensionCallBeforeAddBrighter = extensionCallBeforeAddBrighter;
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
            services.AddScoped<IOrderDbContext, OrderDbContext>();
            services.AddSingleton<OrderDbContextRecorder>();

            void AddExtension()
            {
                if (_extensionAffinityArgument is { } affinity)
                    services.AddBrighterRequestScope(affinity);
                else
                    services.AddBrighterRequestScope();
            }

            void AddBrighterCore()
            {
                services.AddBrighter(options =>
                {
                    options.HandlerLifetime = ServiceLifetime.Scoped;
                    options.MapperLifetime = ServiceLifetime.Scoped;
                    options.TransformerLifetime = ServiceLifetime.Scoped;
                    options.DefaultScopeAffinity = _hostDefaultAffinity;
                });
            }

            if (_extensionCallBeforeAddBrighter)
            {
                AddExtension();
                AddBrighterCore();
            }
            else
            {
                AddBrighterCore();
                AddExtension();
            }

            services.AddSingleton<IAmAScopeProvider, DelegatingScopeProviderRecorder>();
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
