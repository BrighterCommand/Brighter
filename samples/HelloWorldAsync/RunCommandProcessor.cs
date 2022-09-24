using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;

namespace HelloWorldAsync
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
            await _commandProcessor.SendAsync(new GreetingCommand("Ian"), cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
