using System.Threading;
using System.Threading.Tasks;
using GreetingsApp.Entities;
using GreetingsApp.EntityGateway;
using GreetingsApp.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers
{
    public class AddPersonHandlerAsync(GreetingsEntityGateway uow) : RequestHandlerAsync<AddPerson>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddPerson> HandleAsync(AddPerson addPerson, CancellationToken cancellationToken = default)
        {
            uow.Add(new Person(addPerson.Name));
            
            await uow.SaveChangesAsync(cancellationToken);
            
            return await base.HandleAsync(addPerson, cancellationToken);
        }
    }
}
