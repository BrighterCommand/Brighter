using GreetingsEntities;
using GreetingsPorts.Handlers;
using Marten;
using Marten.Schema;
using Npgsql;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.RMQ;
using Paramore.Darker.AspNetCore;
using Polly;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly.Registry;

namespace GreetingsWeb.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            ConfigureMarten(services, configuration);
            ConfigureDarker(services);
            ConfigureBrighter(services);
            return services;
        }

        private static void ConfigureMarten(IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("MartenDb");

            services.AddMarten(opts =>
            {
                opts.Connection(connectionString);
                opts.UseDefaultSerialization(nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters);
                opts.RetryPolicy(MartenRetryPolicy.Twice(exception => exception is NpgsqlException ne && ne.IsTransient));
                opts.Schema.For<Person>()
                .SoftDeleted()
                .UniqueIndex(UniqueIndexType.Computed, x => x.Name);
            })
            .OptimizeArtifactWorkflow()
            .UseLightweightSessions();
        }

        private static void ConfigureDarker(IServiceCollection services)
        {
            services.AddDarker(options =>
            {
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.QueryProcessorLifetime = ServiceLifetime.Scoped;
            })
            .AddHandlersFromAssemblies(typeof(FindPersonByNameHandlerAsync).Assembly);
        }

        private static void ConfigureBrighter(IServiceCollection services)
        {
            var retryPolicy = Policy.Handle<Exception>()
                .WaitAndRetry(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var retryPolicyAsync = Policy.Handle<Exception>()
                .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150) });
            var circuitBreakerPolicy = Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(500));
            var circuitBreakerPolicyAsync = Policy.Handle<Exception>().CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(500));

            var policyRegistry = new PolicyRegistry()
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicyAsync },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicyAsync }
            };

            services.AddBrighter(options =>
            {
                // we want to use scoped, so make sure everything understands that which needs to
                options.HandlerLifetime = ServiceLifetime.Scoped;
                options.CommandProcessorLifetime = ServiceLifetime.Scoped;
                options.MapperLifetime = ServiceLifetime.Singleton;
                options.PolicyRegistry = policyRegistry;
            })
            .UseExternalBus(new RmqProducerRegistryFactory(
                new RmqMessagingGatewayConnection
                {
                    AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
                    Exchange = new Exchange("paramore.brighter.exchange"),
                },
                new RmqPublication[]
                {
                    new RmqPublication
                    {
                        Topic = new RoutingKey("GreetingMade"),
                        MaxOutStandingMessages = 5,
                        MaxOutStandingCheckIntervalMilliSeconds = 500,
                        WaitForConfirmsTimeOutInMilliseconds = 1000,
                        MakeChannels = OnMissingChannel.Create
                    }}
                ).Create())
            .AutoFromAssemblies();
        }
    }
}
