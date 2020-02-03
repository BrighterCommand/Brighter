using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Xunit;

namespace Tests
{
    public class AssemblyResolutionHandlerLifetimeScopedAndMapperLifetimeSingletonTests
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _services;

        public AssemblyResolutionHandlerLifetimeScopedAndMapperLifetimeSingletonTests()
        {
            _services = new ServiceCollection();

            _services.AddServiceActivator(options => { options.CommandProcessorLifetime = ServiceLifetime.Scoped; })
                .AutoFromAssemblies();

            _provider = _services.BuildServiceProvider();
        }

        [Fact]
        public void ShouldHaveCommandProcessorRegisteredCorrectly()
        {
            TestRegistration(typeof(IAmACommandProcessor), ServiceLifetime.Scoped);
        }

        [Fact]
        public void ShouldHaveServiceActivatorRegisteredCorrectly()
        {
            TestRegistration(typeof(IDispatcher), ServiceLifetime.Singleton);
        }

        [Fact]
        public void ShouldHaveTestHandlerRegisteredCorrectly()
        {
            TestRegistration(typeof(TestEventHandler), ServiceLifetime.Transient);
        }

        [Fact]
        public void ShouldHaveTestMapperRegisteredCorrectly()
        {
            TestRegistration(typeof(TestEventMessageMapper), ServiceLifetime.Singleton);
        }

        [Fact]
        public void ShouldHaveDefaultHandlerRegisteredCorrectly()
        {
            TestRegistration(typeof(ExceptionPolicyHandler<>), ServiceLifetime.Transient);
            TestRegistration(typeof(FallbackPolicyHandler<>), ServiceLifetime.Transient);
            TestRegistration(typeof(TimeoutPolicyHandler<>), ServiceLifetime.Transient);
            TestRegistration(typeof(MonitorHandler<>), ServiceLifetime.Transient);
            TestRegistration(typeof(UseInboxHandler<>), ServiceLifetime.Transient);
            TestRegistration(typeof(RequestLoggingHandler<>), ServiceLifetime.Transient);
        }

        private void TestRegistration(Type expected, ServiceLifetime serviceLifetime)
        {
            var serviceDescriptor = _services.SingleOrDefault(x => x.ServiceType == expected);

            Assert.Equal(expected, serviceDescriptor.ServiceType);
            Assert.Equal(serviceLifetime, serviceDescriptor.Lifetime);
        }


        [Fact]
        public void ShouldHaveCommandProcessor()
        {
            Assert.Equal(typeof(CommandProcessor), _provider.GetService<IAmACommandProcessor>().GetType());
        }

        [Fact]
        public void ShouldHaveHandler()
        {
            Assert.NotNull(_provider.GetService<TestEventHandler>());
        }

        [Fact]
        public void ShouldHaveMapper()
        {
            Assert.NotNull(_provider.GetService<TestEventMessageMapper>());
        }
    }
}
