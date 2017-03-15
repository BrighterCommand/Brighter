using System;
using System.Threading.Tasks;
using HelloAsyncListeners.TinyIoc;
using Paramore.Brighter;

namespace HelloAsyncListeners
{
    internal class Program
    {
        private static void Main()
        {
            var container = new TinyIoCContainer();
            var asyncHandlerFactory = new TinyIocHandlerFactory(container);

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<GreetingEvent, WantToBeGreeted>();
            registry.RegisterAsync<GreetingEvent, WantToBeGreetedToo>();

            var builder = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, asyncHandlerFactory))
                .DefaultPolicy()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory());

            var commandProcessor = builder.Build();

            // Use bootstrappers for async/await in Console app
            MainAsync(commandProcessor).Wait();

            Console.ReadLine();
        }

        private static async Task MainAsync(IAmACommandProcessor commandProcessor)
        {
            // To allow the event handler(s) to release the thread
            // while doing potentially long running operations,
            // await the async (but inline) handling of the events
            await commandProcessor.PublishAsync(new GreetingEvent("Ian"));

            try
            {
                // This will cause an exception in one event handler
                await commandProcessor.PublishAsync(new GreetingEvent("Roger"));
            }
            catch (AggregateException e)
            {
                Console.WriteLine("Aggregate exception thrown after event Roger:");
                ShowExceptions(e, 1);
            }

            try
            {
                // This will cause an exception in both event handlers
                await commandProcessor.PublishAsync(new GreetingEvent("Devil"));
            }
            catch (AggregateException e)
            {
                Console.WriteLine("Aggregate exception thrown after event Devil:");
                ShowExceptions(e, 1);
            }
        }

        private static void ShowExceptions(AggregateException e, int indent)
        {
            foreach (var innerException in e.InnerExceptions)
            {
                for (var i = 0; i < indent; i++)
                    Console.Write("  ");
                Console.WriteLine(innerException.Message);
                if (innerException is AggregateException)
                {
                    ShowExceptions(innerException as AggregateException, indent + 1);
                }
            }
        }

    }
}
