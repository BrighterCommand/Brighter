using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Command;
using Greetings.Ports.Events;
using GreetingsSender.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;

namespace GreetingsSender.Web.Handlers.Command
{
    public class PutGreetingDirectIntoDbCommandHandler : RequestHandlerAsync<PutGreetingDirectIntoDbCommand>
    {
        private readonly GreetingsDataContext _context;
        private readonly ILogger<PutGreetingDirectIntoDbCommandHandler> _logger;

        public PutGreetingDirectIntoDbCommandHandler(GreetingsDataContext context, ILogger<PutGreetingDirectIntoDbCommandHandler> logger)
        {
            _context = context;
            _logger = logger;
        }
        
        public override async Task<PutGreetingDirectIntoDbCommand> HandleAsync(PutGreetingDirectIntoDbCommand command,
            CancellationToken cancellationToken = new())
        {
            await _context.Greetings.AddAsync(command.Greeting, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Greeting Added to Db");
            
            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
