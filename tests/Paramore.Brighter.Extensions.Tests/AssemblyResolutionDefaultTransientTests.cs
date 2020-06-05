using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Tests;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests
{
    public class AssemblyResolutionDefaultTransientTests
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _services;

        public AssemblyResolutionDefaultTransientTests()
        {
            _services = new ServiceCollection();

            _services.AddServiceActivator().AutoFromAssemblies();
              
            _provider = _services.BuildServiceProvider();
        }

        [Fact]
        public void ShouldHaveCommandProcessorRegisteredCorrectly()
        {
            TestRegistration(typeof(IAmACommandProcessor), ServiceLifetime.Singleton);
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
        public void ShouldHaveServiceActivator()
        {
            Assert.Equal(typeof(Dispatcher), _provider.GetService<IDispatcher>().GetType());
        } 

    }
}
