using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;

namespace HelloAsyncListeners
{
    internal class RunCommandProcessor : IHostedService
    {
        private readonly IAmACommandProcessor _commandProcessor;

        public RunCommandProcessor(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // To allow the event handler(s) to release the thread
            // while doing potentially long running operations,
            // await the async (but inline) handling of the events
            await _commandProcessor.PublishAsync(new GreetingEvent("Ian"), cancellationToken: cancellationToken);


            try
            {
                // This will cause an exception in one event handler
                await _commandProcessor.PublishAsync(new GreetingEvent("Roger"), cancellationToken: cancellationToken);
            }
            catch (AggregateException e)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine("Aggregate exception thrown after event Roger:");
                ShowExceptions(e, 1);

                Console.ResetColor();
            }

            try
            {
                // This will cause an exception in both event handlers
                await _commandProcessor.PublishAsync(new GreetingEvent("Devil"), cancellationToken: cancellationToken);
            }
            catch (AggregateException e)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine("Aggregate exception thrown after event Devil:");
                ShowExceptions(e, 1);

                Console.ResetColor();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static void ShowExceptions(AggregateException e, int indent)
        {
            foreach (var innerException in e.InnerExceptions)
            {
                for (var i = 0; i < indent; i++)
                    Console.Write("  ");
                Console.WriteLine(innerException.Message);
                if (innerException is AggregateException exception)
                    ShowExceptions(exception, indent + 1);
            }
        }
    }
}
