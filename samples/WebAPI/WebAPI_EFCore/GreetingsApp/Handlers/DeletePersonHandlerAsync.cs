using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsApp.EntityGateway;
using GreetingsApp.Requests;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly GreetingsEntityGateway _uow;

        public DeletePersonHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            var person = await _uow.Person
                .Include(p => p.Greetings)
                .Where(p => p.Name == deletePerson.Name)
                .SingleAsync(cancellationToken);

            _uow.Remove(person);

            await _uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
