using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.Fakes;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Tests
{
    public class ResolveDispatcherWithScopedRegisteredTransactionProvider
    {
        private IServiceProvider _provider;

        public void Build()
        {
            var services = new ServiceCollection();

            AddServices(services);

            _provider = services.BuildServiceProvider();
        }

        public void BuildHost()
        {
            _provider = Host.CreateDefaultBuilder([])
                .ConfigureServices((hostContext, services) =>
                {
                    AddServices(services);

                }).Build().Services;
        }

        private void AddServices(IServiceCollection services)
        {
            services.AddScoped<FakeDbContext>();

            services
                .AddBrighter()
                .AddProducers(configure =>
                {
                    configure.ProducerRegistry = new ProducerRegistry(
                        new Dictionary<ProducerKey, IAmAMessageProducer>()
                        {
                            { new ProducerKey("greeting.event"), new FakeMessageProducer() }
                        });
                    configure.TransactionProvider = typeof(FakeTransactionProvider<FakeDbContext>);
                    configure.ConnectionProvider = typeof(FakeConnectionProvider);
                    configure.MaxOutStandingMessages = 5;
                    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
                })
                .AutoFromAssemblies();

            services.AddConsumers(options =>
            {
                options.UseScoped = true;
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Singleton;
                options.CommandProcessorLifetime = ServiceLifetime.Scoped;
            });
        }

        [Fact]
        public void ShouldResolveIDispatcherCorrectly()
        {
            Build();

            var dispatcher = _provider.GetRequiredService<IDispatcher>();
        }

        [Fact]
        public void ShouldResolveIDispatcherCorrectlyWithHost()
        {
            BuildHost();

            var dispatcher = _provider.GetRequiredService<IDispatcher>();
        }
    }
}
