using System;
using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
    {
        private readonly GreetingsEntityGateway _uow;

        public AddGreetingHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }

        public override async Task<AddGreeting> HandleAsync(AddGreeting command, CancellationToken cancellationToken = default)
        {
            try
            {
                var greeting = new Greeting(command.Greeting);
                // send the message via RMQ to consumer

                //await _uow.SaveChangesAsync();
            }
            catch (Exception)
            {

            }
            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
