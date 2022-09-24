using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;

namespace GreetingsPorts.Handlers
{
    public class AddPersonHandlerAsync : RequestHandlerAsync<AddPerson>
    {
        private readonly IUnitOfWork _uow;

        public AddPersonHandlerAsync(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            await _uow.Database.InsertAsync<Person>(new Person(addPerson.Name));
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
