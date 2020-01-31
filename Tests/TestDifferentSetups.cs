using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Xunit;

namespace Tests
{
    public class TestBrighterExtension
    {
        [Fact]
        public void BasicSetup()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddBrighter().AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void WithProducer()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection
                .AddBrighter(options => 
                    options.BrighterMessaging = new BrighterMessaging(new InMemoryMessageStore(), new FakeProducer()))
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        
        [Fact]
        public void WithCustomPolicy()
        {
            var serviceCollection = new ServiceCollection();

            var retryPolicy = Policy.Handle<Exception>().WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var retryPolicyAsync = Policy.Handle<Exception>().WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));
            var policyRegistry = new PolicyRegistry()
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            
            serviceCollection
                .AddBrighter(options => options.PolicyRegistry = policyRegistry)
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

        [Fact]
        public void WithScopedLifetime()
        {
            var serviceCollection = new ServiceCollection();
            
            serviceCollection.AddBrighter(options => options.CommandProcessorLifetime = ServiceLifetime.Scoped
                ).AutoFromAssemblies();

            Assert.Equal( ServiceLifetime.Scoped, serviceCollection.SingleOrDefault(x => x.ServiceType == typeof(IAmACommandProcessor))?.Lifetime);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();
            
            Assert.NotNull(commandProcessor);
        }

    }

    internal class FakeProducer : IAmAMessageProducer
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Send(Message message)
        {
            throw new NotImplementedException();
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            throw new NotImplementedException();
        }
    }
}
