using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly GreetingsEntityGateway _uow;

        public DeletePersonHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }
        public async override Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            //var person = await _uow.People
            //    .Include(p => p.Greetings)
            //    .Where(p => p.Name == deletePerson.Name)
            //    .SingleAsync(cancellationToken);

            //_uow.Remove(person);

            // await _uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
