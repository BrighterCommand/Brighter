using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _services;

        public ResolveDispatcherWithScopedRegisteredTransactionProvider()
        {
            _services = new ServiceCollection();

            _services.AddScoped<FakeDbContext>();

            _services
                .AddBrighter()
                .AddProducers(configure =>
                {
                    configure.ProducerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer>()
                    {
                        {
                            new ProducerKey("greeting.event"),
                            new FakeMessageProducer()
                        }
                    });
                    configure.TransactionProvider = typeof(FakeTransactionProvider<FakeDbContext>);
                    configure.ConnectionProvider = typeof(FakeConnectionProvider);
                    configure.MaxOutStandingMessages = 5;
                    configure.MaxOutStandingCheckInterval = TimeSpan.FromMilliseconds(500);
                })
                .AutoFromAssemblies();

            _services.AddConsumers(options => { options.CommandProcessorLifetime = ServiceLifetime.Scoped; });

            _provider = _services.BuildServiceProvider();
        }

        [Fact]
        public void ShouldResolveIDispatcherCorrectly()
        {
            var dispatcher = _provider.GetRequiredService<IDispatcher>();
        }
    }
}
