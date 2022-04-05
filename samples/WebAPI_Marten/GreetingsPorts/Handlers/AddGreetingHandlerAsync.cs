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
            var person = await _uow.GetByName(command.Name);
            var greeting = new Greeting
            {
                Message = command.Greeting,
            };

            person.AddGreeting(greeting);
            _uow.Update(person);

            await _uow.CommitChanges();

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
