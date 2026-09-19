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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.AspNetCore;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An ASP.NET test host whose ambient source is a hand-rolled <see cref="AffinityIgnoringScopeProvider"/>,
/// registered as the only <see cref="IAmAScopeProvider"/> descriptor with the affinity option set to
/// <see cref="ScopeAffinity.JoinAmbient"/> directly on <see cref="IBrighterOptions.DefaultScopeAffinity"/>
/// - no call to <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/> is made at all (AC-11).
/// Lifetime triple <c>{Scoped, Scoped, Scoped}</c>. Hosts <see cref="PlaceOrderController"/> (a <c>Send</c>)
/// and <see cref="PublishOrderController"/> (a <c>PublishAsync</c> to two subscribers).
/// </summary>
public sealed class AffinityIgnoringProviderWebApplicationFactory : WebApplicationFactory<PlaceOrderController>
{
    private readonly bool _offerForAlwaysNew;
    private readonly bool _offerForJoinAmbient;

    /// <param name="offerForAlwaysNew">Whether the provider offers the request's ambient for an
    /// <see cref="ScopeAffinity.AlwaysNew"/> ask.</param>
    /// <param name="offerForJoinAmbient">Whether the provider offers the request's ambient for a
    /// <see cref="ScopeAffinity.JoinAmbient"/> ask.</param>
    public AffinityIgnoringProviderWebApplicationFactory(bool offerForAlwaysNew, bool offerForJoinAmbient)
    {
        _offerForAlwaysNew = offerForAlwaysNew;
        _offerForJoinAmbient = offerForJoinAmbient;
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
            services.AddHttpContextAccessor();
            services.AddScoped<IOrderDbContext, OrderDbContext>();
            services.AddSingleton<OrderDbContextRecorder>();
            services.AddSingleton<PublishScopeRecorder>();
            services.AddSingleton<IAmAScopeProvider>(sp => new AffinityIgnoringScopeProvider(
                sp.GetRequiredService<IHttpContextAccessor>(),
                _offerForAlwaysNew,
                _offerForJoinAmbient));

            services.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
                options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
            });
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
