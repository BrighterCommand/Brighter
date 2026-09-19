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
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An ASP.NET test host opted in to <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>
/// with <see cref="ScopeAffinity.JoinAmbient"/> given as the argument (AC-55), lifetime triple
/// <c>{Scoped, Scoped, Scoped}</c>, a <see cref="DelegatingScopeProviderRecorder"/> registered last so it
/// is the effective ambient source, and one controller,
/// <see cref="StartDispatcherFromRequestController"/>, that starts a real <c>Dispatcher</c> from inside its
/// own action rather than at host startup. Its <see cref="Producer"/> and <see cref="RoutingKey"/> are
/// built once, in the constructor, so a test can push messages onto the same <c>InternalBus</c> the app's
/// own <c>InMemoryChannelFactory</c> reads from regardless of how many times ASP.NET Core's own test host
/// re-runs <c>ConfigureServices</c> (T6.10's discovery-host quirk).
/// </summary>
public sealed class DispatcherFromRequestWebApplicationFactory : WebApplicationFactory<StartDispatcherFromRequestController>
{
    /// <summary>
    /// The number of messages a test sends before calling the one endpoint this host exposes.
    /// </summary>
    public const int MessageCount = 10;

    private readonly RoutingKey _routingKey = new("dispatcher-from-request.consumer");
    private readonly InternalBus _bus = new();
    private readonly InMemoryChannelFactory _channelFactory;
    private readonly InMemoryMessageProducer _producer;
    private readonly CapturingLoggerProvider _capturingLoggerProvider = new();

    /// <summary>
    /// The producer a test sends <see cref="DispatcherFromRequestCommand"/> messages through, onto the
    /// same bus the app's own consumer subscription reads from.
    /// </summary>
    public IAmAMessageProducerSync Producer => _producer;

    /// <summary>
    /// The routing key the app's own consumer subscription listens on.
    /// </summary>
    public RoutingKey RoutingKey => _routingKey;

    /// <summary>
    /// Every log entry captured while this host was running.
    /// </summary>
    public IReadOnlyCollection<CapturedLogEntry> LogEntries => _capturingLoggerProvider.Entries;

    public DispatcherFromRequestWebApplicationFactory()
    {
        _channelFactory = new InMemoryChannelFactory(_bus, TimeProvider.System);
        _producer = new InMemoryMessageProducer(_bus, new Publication { Topic = _routingKey, RequestType = typeof(DispatcherFromRequestCommand) });
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
            services.AddControllers().AddApplicationPart(typeof(StartDispatcherFromRequestController).Assembly);
            services.AddSingleton<DispatcherFromRequestRecorder>();
            services.AddSingleton(new CountdownEvent(MessageCount));
            services.AddLogging(builder => builder.AddProvider(_capturingLoggerProvider));

            services.AddBrighterRequestScope(ScopeAffinity.JoinAmbient);

            services.AddConsumers(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Scoped;
                options.TransformerLifetime = ServiceLifetime.Scoped;
                options.Subscriptions = new List<Subscription>
                {
                    new(
                        new SubscriptionName("dispatcher-from-request-consumer"),
                        new ChannelName("dispatcher-from-request-consumer:in-memory"),
                        _routingKey,
                        typeof(DispatcherFromRequestCommand),
                        messagePumpType: MessagePumpType.Reactor)
                };
                options.DefaultChannelFactory = _channelFactory;
            });

            services.AddSingleton<IAmAScopeProvider, DelegatingScopeProviderRecorder>();
        });

        builder.Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        });
    }
}
