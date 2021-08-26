using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsInteractors.EntityGateway;
using GreetingsInteractors.Requests;
using Paramore.Brighter;

namespace GreetingsInteractors.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly GreetingsEntityGateway _uow;
        private readonly IAmACommandProcessor _postBox;

        public AddPersonHandlerAsync(GreetingsEntityGateway uow, IAmACommandProcessor postBox)
        {
            _uow = uow;
            _postBox = postBox;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            var person = new Person(addPerson.Name);

            _uow.Add(person);
            
            await _uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
