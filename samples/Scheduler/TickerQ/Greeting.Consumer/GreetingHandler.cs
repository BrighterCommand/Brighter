using Greeting.Models;
using Paramore.Brighter;

namespace Greeting.Consumer
{
    public class GreetingHandler : RequestHandlerAsync<GreetingEvent>
    {
        private readonly ILogger<GreetingHandler> _logger;

        public GreetingHandler(ILogger<GreetingHandler> logger)
        {
            _logger = logger;
        }
        public override Task<GreetingEvent> HandleAsync(GreetingEvent command, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Hello {Name}", command.Name);
            return base.HandleAsync(command, cancellationToken);
        }

    }
}
