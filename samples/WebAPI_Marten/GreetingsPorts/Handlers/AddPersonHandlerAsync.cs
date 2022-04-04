using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly GreetingsEntityGateway _uow;

        public AddPersonHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson command, CancellationToken cancellationToken = default)
        {
            _uow.Add(new Person(command.Name));
            await _uow.CommitChanges();

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
