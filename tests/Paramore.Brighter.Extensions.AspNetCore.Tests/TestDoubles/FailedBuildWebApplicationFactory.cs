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
/// (AC-38's setup): lifetime triple <c>{Scoped, Scoped, Scoped}</c>, <see cref="IOrderDbContext"/>
/// registered <c>AddScoped</c>, and a mapper (<see cref="FailedBuildPostedMapper"/>) whose constructor
/// dependency is never registered, so every pipeline build for <see cref="FailedBuildPostedCommand"/>
/// fails. A <see cref="DelegatingScopeProviderRecorder"/> is registered last so a test can confirm every
/// ask carried <see cref="ScopeAffinity.JoinAmbient"/>.
/// </summary>
public sealed class FailedBuildWebApplicationFactory : WebApplicationFactory<FailedBuildController>
{
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
            services.AddControllers().AddApplicationPart(typeof(FailedBuildController).Assembly);
            services.AddScoped<IOrderDbContext, OrderDbContext>();
            services.AddSingleton<FailedBuildRecorder>();

            var routingKey = new RoutingKey("failed-build");
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(FailedBuildPostedCommand) }) }
            });

            services.AddBrighterRequestScope();
            services.AddBrighter(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
            })
            .AddProducers(cfg => cfg.ProducerRegistry = producerRegistry);

            services.AddSingleton<IAmAScopeProvider, DelegatingScopeProviderRecorder>();
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
