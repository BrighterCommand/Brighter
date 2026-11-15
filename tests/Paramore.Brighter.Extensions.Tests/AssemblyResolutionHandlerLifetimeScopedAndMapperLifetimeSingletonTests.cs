using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;

namespace Tests
{
    public class AssemblyResolutionHandlerLifetimeScopedAndMapperLifetimeSingletonTests
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _services;

        public AssemblyResolutionHandlerLifetimeScopedAndMapperLifetimeSingletonTests()
        {
            _services = new ServiceCollection();

            _services.AddConsumers()
                .AutoFromAssemblies();

            _provider = _services.BuildServiceProvider();
        }

        [Test]
        public async Task ShouldHaveCommandProcessorRegisteredCorrectly()
        {
            await TestRegistration(typeof(IAmACommandProcessor), ServiceLifetime.Singleton);
        }

        [Test]
        public async Task ShouldHaveServiceActivatorRegisteredCorrectly()
        {
            await TestRegistration(typeof(IDispatcher), ServiceLifetime.Singleton);
        }

        [Test]
        public async Task ShouldHaveTestHandlerRegisteredCorrectly()
        {
            await TestRegistration(typeof(TestEventHandler), ServiceLifetime.Transient);
        }

        [Test]
        public async Task ShouldHaveTestMapperRegisteredCorrectly()
        {
            await TestRegistration(typeof(TestEventMessageMapper), ServiceLifetime.Transient);
        }

        [Test]
        public async Task ShouldHaveDefaultHandlerRegisteredCorrectly()
        {
            await TestRegistration(typeof(ExceptionPolicyHandler<>), ServiceLifetime.Transient);
            await TestRegistration(typeof(FallbackPolicyHandler<>), ServiceLifetime.Transient);
            await TestRegistration(typeof(TimeoutPolicyHandler<>), ServiceLifetime.Transient);
            await TestRegistration(typeof(MonitorHandler<>), ServiceLifetime.Transient);
            await TestRegistration(typeof(UseInboxHandler<>), ServiceLifetime.Transient);
            await TestRegistration(typeof(RequestLoggingHandler<>), ServiceLifetime.Transient);
        }

        private async Task TestRegistration(Type expected, ServiceLifetime serviceLifetime)
        {
            var serviceDescriptor = _services.SingleOrDefault(x => x.ServiceType == expected);

            await Assert.That(serviceDescriptor.ServiceType).IsEqualTo(expected);
            await Assert.That(serviceDescriptor.Lifetime).IsEqualTo(serviceLifetime);
        }


        [Test]
        public async Task ShouldHaveCommandProcessor()
        {
            await Assert.That(_provider.GetService<IAmACommandProcessor>().GetType()).IsEqualTo(typeof(CommandProcessor));
        }

        [Test]
        public async Task ShouldHaveHandler()
        {
            await Assert.That(_provider.GetService<TestEventHandler>()).IsNotNull();
        }

        [Test]
        public async Task ShouldHaveMapper()
        {
            await Assert.That(_provider.GetService<TestEventMessageMapper>()).IsNotNull();
        }
    }
}
