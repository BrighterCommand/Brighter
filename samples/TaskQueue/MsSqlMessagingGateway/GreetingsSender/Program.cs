using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using Serilog;
using Serilog.Extensions.Logging;

namespace GreetingsSender
{
    static class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILoggerFactory>(new SerilogLoggerFactory());

            var messagingConfiguration = new RelationalDatabaseConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", queueStoreTable: "QueueData");

            var producerRegistry = new MsSqlProducerRegistryFactory(
                    messagingConfiguration,
                    [new Publication()]
                )
                .Create();
            
            serviceCollection.AddBrighter()
                .UseExternalBus((configure) =>
                {
                    configure.ProducerRegistry = producerRegistry;
                })
                .AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();


            commandProcessor.Post(new GreetingEvent("Ian"));
        }
    }
}
