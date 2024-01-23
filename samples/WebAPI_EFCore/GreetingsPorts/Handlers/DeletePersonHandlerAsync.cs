using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync(GreetingsEntityGateway uow) : RequestHandlerAsync<DeletePerson>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            var person = await uow.People
                .Include(p => p.Greetings)
                .Where(p => p.Name == deletePerson.Name)
                .SingleAsync(cancellationToken);

            uow.Remove(person);

            await uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
