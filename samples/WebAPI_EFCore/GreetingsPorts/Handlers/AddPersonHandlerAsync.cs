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

        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            _uow.Add(new Person(addPerson.Name));
            
            await _uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
