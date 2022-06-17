using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        public AddPersonHandlerAsync()
        {
        }

        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            //await _uow.Database.InsertAsync<Person>(new Person(addPerson.Name));
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
