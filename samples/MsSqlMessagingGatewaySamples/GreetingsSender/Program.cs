using Events.Ports.Commands;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MessagingGateway.MsSql;
using Serilog;

namespace GreetingsSender
{
    class Program
    {
        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            var serviceCollection = new ServiceCollection();

            var messagingConfiguration = new MsSqlMessagingGatewayConfiguration(@"Database=BrighterSqlQueue;Server=.\sqlexpress;Integrated Security=SSPI;", "QueueData");
            var producer = new MsSqlMessageProducer(messagingConfiguration);

            serviceCollection.AddBrighter(options =>
            {
                var outBox = new InMemoryOutbox();
                options.BrighterMessaging = new BrighterMessaging(outBox, outBox, producer, null);
            }).AutoFromAssemblies();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var commandProcessor = serviceProvider.GetService<IAmACommandProcessor>();


            commandProcessor.Post(new GreetingEvent("Ian"));
        }
    }
}
