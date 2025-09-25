using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsApp.EntityGateway;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using GreetingsApp.Responses;
using Microsoft.EntityFrameworkCore;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsApp.Handlers
{
    public class FIndGreetingsForPersonHandlerAsync(GreetingsEntityGateway uow)
        : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings?>
    {
        [QueryLogging(0)] 
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonsGreetings?> ExecuteAsync(FindGreetingsForPerson query, CancellationToken cancellationToken = new CancellationToken())
        {
            var person = await uow.People
                .Include(p => p.Greetings)
                .Where(p => p.Name == query.Name)
                .SingleAsync(cancellationToken);

            if (person == null) return null;

            return new FindPersonsGreetings
            {
                Name = person.Name, 
                Greetings = person.Greetings.Select(g => new Salutation(g.Greet()))
            };

        }
        
    }
}
